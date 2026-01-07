using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using DiscordPA.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace DiscordPA.Commands;

public class TldrModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AiSummarizerService _summarizer;
    private readonly SummaryCacheService _cache;
    private readonly CostTrackerService _costTracker;
    private readonly SpamBlockerService _spamBlocker;
    private readonly GuildAccessService _access;
    private readonly ILogger<TldrModule> _logger; // Injected logger

    private static readonly Dictionary<string, int> DepthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "recent", 100 },
        { "brief", 200 },
        { "standard", 300 },
        { "deep", 400 },
        { "max", 500 }
    };

    // Timeout for message fetch operations
    private static readonly TimeSpan MessageFetchTimeout = TimeSpan.FromSeconds(15);

    // Discord embed description limit
    private const int EmbedDescriptionLimit = 4096;

    // Retry settings for Discord API throttling
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    public TldrModule(
        AiSummarizerService summarizer,
        SummaryCacheService cache,
        CostTrackerService costTracker,
        GuildAccessService access,
        SpamBlockerService spamBlocker,
        ILogger<TldrModule> logger)
    {
        _summarizer = summarizer;
        _cache = cache;
        _costTracker = costTracker;
        _access = access;
        _spamBlocker = spamBlocker;
        _logger = logger;
    }

    [SlashCommand("tldr", "Summarize recent messages by depth")]
    public async Task TldrAsync(
       [Summary(description: "Summary depth: recent, brief, standard, deep, or max")] string depth,
       [Summary(description: "Optional user to filter")] IUser? user = null)
    {
        // Create a logging scope with additional context
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = Context.Guild?.Id ?? 0,
            ["GuildName"] = Context.Guild?.Name ?? "DM",
            ["ChannelId"] = Context.Channel.Id,
            ["ChannelName"] = Context.Channel.Name,
            ["InvokerId"] = Context.User.Id,
            ["InvokerName"] = Context.User.Username
        }))
        {
            if (!DepthMap.TryGetValue(depth, out var messageLimit))
            {
                _logger.LogWarning("Invalid depth parameter received: {Depth}", depth);
                await RespondAsync("❌ Invalid depth. Try `/tldr help` for valid options.", ephemeral: true);
                return;
            }

            if (Context.Channel is not SocketTextChannel textChannel)
            {
                _logger.LogWarning("Command invoked in a non-text channel.");
                await RespondAsync("❌ This command only works in text channels.", ephemeral: true);
                return;
            }

            var botUser = textChannel.Guild.GetUser(Context.Client.CurrentUser.Id);
            if (botUser == null)
            {
                _logger.LogError("Bot user was null in guild {GuildName}", textChannel.Guild.Name);
                await RespondAsync("⚠️ Could not verify bot permissions in this channel.", ephemeral: true);
                return;
            }

            var permissions = botUser.GetPermissions(textChannel);
            var missingPerms = new List<string>();
            if (!permissions.ViewChannel) missingPerms.Add("View Channel");
            if (!permissions.ReadMessageHistory) missingPerms.Add("Read Message History");
            if (!permissions.SendMessages) missingPerms.Add("Send Messages");

            if (missingPerms.Count > 0)
            {
                _logger.LogWarning("Insufficient bot permissions in channel {ChannelName}: {MissingPerms}",
                    textChannel.Name, string.Join(", ", missingPerms));
                await RespondAsync($"🚫 I'm missing these permissions in this channel:\n• {string.Join("\n• ", missingPerms)}\n\nPlease ask a server admin to grant these permissions.", ephemeral: true);
                return;
            }

            // Always defer first to show "thinking..." indicator
            await DeferAsync(ephemeral: true);

            // Warn about potential issues with "max" depth
            if (depth == "max")
            {
                await FollowupAsync("⚠️ `max` depth may result in slower or overly broad summaries. Use `/tldr-help` for more focused tiers.", ephemeral: true);
            }

            _logger.LogInformation("Fetching up to {MessageLimit} messages for depth {Depth}.", messageLimit, depth);

            var messages = await FetchRecentMessages(textChannel, messageLimit);

            var filtered = messages
                .Where(m => !m.Author.IsBot)
                .Where(m => user == null || m.Author.Id == user.Id)
                .OrderBy(m => m.Timestamp)
                .Select(m => $"{m.Author.Username}: {m.Content}")
                .ToList();

            if (!filtered.Any())
            {
                _logger.LogInformation("No messages found after filtering.");
                await FollowupAsync("🕵️ No messages found for that depth and filter.", ephemeral: true);
                return;
            }

            var uniqueLines = filtered.Select(m => m.ToLowerInvariant()).Distinct().Count();
            var repetitionRatio = (double)uniqueLines / filtered.Count;
            _logger.LogDebug("Filtered messages count: {Count}, Unique lines: {Unique}, Repetition ratio: {Ratio:P}",
                filtered.Count, uniqueLines, repetitionRatio);

            if (depth == "max" && repetitionRatio < 0.6)
            {
                _logger.LogInformation("Repetitive messages detected. Repetition ratio: {Ratio:P}", repetitionRatio);
                await FollowupAsync("⚠️ A large portion of messages appear repetitive. Summary may be diluted or vague.", ephemeral: true);
            }

            string summary;
            double cost = 0;
            bool wasCached = false;
            var guildIdStr = Context.Guild?.Id.ToString() ?? "dm";
            var channelIdStr = Context.Channel.Id.ToString();
            string? userIdStr = user == null ? null : user.Id.ToString();

            _logger.LogInformation("Command invoked: /tldr depth:{Depth} user:{User}", depth, user?.Username ?? "none");

            if (_cache.TryGet(guildIdStr, channelIdStr, depth, userIdStr, filtered, out summary, out cost))
            {
                wasCached = true;
                _logger.LogInformation("Cache HIT for command /tldr with depth: {Depth} in channel: {ChannelId}", depth, channelIdStr);

                if (_spamBlocker.IsCachedSpamming(guildIdStr, channelIdStr, Context.User.Id.ToString(), out var spamReason))
                {
                    _logger.LogWarning("Cached spam threshold exceeded: {Reason}", spamReason);
                    await FollowupAsync(spamReason, ephemeral: true);
                    return;
                }
            }
            else
            {
                _logger.LogInformation("Cache MISS for command /tldr with depth: {Depth} in channel: {ChannelId}", depth, channelIdStr);

                var requestingUserId = Context.User.Id;
                var isAdmin = await _access.CanAccessAdminFeaturesAsync(Context.Guild?.Id ?? 0, requestingUserId);

                if (_spamBlocker.IsSpamming(guildIdStr, channelIdStr, requestingUserId.ToString(), isAdmin, wasCached: false, out var spamReason))
                {
                    _logger.LogWarning("Spam check triggered: {Reason}", spamReason);
                    await FollowupAsync(spamReason, ephemeral: true);
                    return;
                }

                try
                {
                    var (generatedSummary, generatedCost) = await _summarizer.SummarizeAsync(filtered);
                    summary = generatedSummary;
                    cost = generatedCost;
                    _cache.Set(guildIdStr, channelIdStr, depth, userIdStr, filtered, summary, cost);

                    _logger.LogInformation("Summarization succeeded with cost: {Cost:C}, summary length: {Length} characters.", cost, summary.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI summarization failed.");
                    await FollowupAsync("🤖 AI summarization failed. Try again later.", ephemeral: true);
                    return;
                }
            }

            // Validate and truncate summary if it exceeds embed description limit
            var displaySummary = summary;
            var wasTruncated = false;
            if (summary.Length > EmbedDescriptionLimit)
            {
                _logger.LogWarning("Summary exceeded embed limit ({Length} chars), truncating to {Limit}",
                    summary.Length, EmbedDescriptionLimit);
                displaySummary = summary[..(EmbedDescriptionLimit - 50)] + "\n\n*... (truncated due to length)*";
                wasTruncated = true;
            }

            // Build cache status indicator
            var cacheStatus = wasCached ? "⚡ Cached (instant & free)" : "🆕 Fresh summary";

            var footerText = $"{cacheStatus} • {(user != null ? $"Filtered: {user.Username}" : "All users")} • " +
                             $"Cost: ${cost:F4} • Total: ${_costTracker.GetTotal():F2}";

            // Color coding: Green=cached, Purple=fresh, Orange=truncated, DarkRed=max depth
            var embedColor = wasTruncated ? Color.Orange
                           : wasCached ? Color.Green
                           : depth == "max" ? Color.DarkRed
                           : Color.DarkPurple;

            var titleSuffix = wasTruncated ? " (Truncated)" : wasCached ? " ⚡" : "";

            var embed = new EmbedBuilder()
                .WithTitle($"TL;DRkseid Summary – {depth.ToUpper()}{titleSuffix}")
                .WithDescription(displaySummary)
                .WithColor(embedColor)
                .WithFooter(new EmbedFooterBuilder { Text = footerText });

            var builder = new ComponentBuilder()
                .WithButton("Buy Me a Coffee ☕", style: ButtonStyle.Link, url: "https://buymeacoffee.com/mcarthey");

            _logger.LogInformation("Sending summary response to user.");
            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }
    }

    [SlashCommand("tldr-help", "Show TLDRkseid commands and options")]
    public async Task TldrHelpAsync()
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = Context.Guild?.Id ?? 0,
            ["GuildName"] = Context.Guild?.Name ?? "DM",
            ["ChannelId"] = Context.Channel.Id,
            ["ChannelName"] = Context.Channel.Name,
            ["InvokerId"] = Context.User.Id,
            ["InvokerName"] = Context.User.Username
        }))
        {
            var embed = new EmbedBuilder()
                .WithTitle("🧠 TL;DRkseid Help")
                .WithDescription("AI-powered conversation summaries for Discord.")
                .WithColor(Color.Teal)
                .AddField("📋 Basic Usage",
                    "`/tldr depth:standard` - Summarize recent messages\n" +
                    "`/tldr depth:brief user:@someone` - Summarize one person's messages",
                    inline: false)
                .AddField("📊 Depth Levels",
                    "**recent** (~100 msgs) - Quick skim\n" +
                    "**brief** (~200 msgs) - Short break catch-up\n" +
                    "**standard** (~300 msgs) - Daily check-in ⭐\n" +
                    "**deep** (~400 msgs) - Extended absence\n" +
                    "**max** (~500 msgs) - Full deep-dive ⚠️",
                    inline: false)
                .AddField("💡 Tips",
                    "• Summaries are private (only you see them)\n" +
                    "• Results are cached for 1 hour to save costs\n" +
                    "• Rate limited to prevent spam (30s cooldown)",
                    inline: false)
                .AddField("🔧 Admin Commands",
                    "Server admins can use `!admin` commands.\n" +
                    "Type `!admin` in chat to see options.",
                    inline: false)
                .WithFooter("TLDRkseid • github.com/mcarthey/Discord-TLDRkseid");

            _logger.LogInformation("Providing tldr-help response.");
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    private async Task<List<IMessage>> FetchRecentMessages(SocketTextChannel channel, int count)
    {
        var messages = new List<IMessage>();
        ulong? beforeMessageId = null;

        using var cts = new CancellationTokenSource(MessageFetchTimeout);

        try
        {
            while (messages.Count < count)
            {
                cts.Token.ThrowIfCancellationRequested();

                var batch = await FetchMessagesWithRetryAsync(channel, beforeMessageId, cts.Token);

                if (!batch.Any()) break;

                messages.AddRange(batch);
                beforeMessageId = batch.Min(m => m.Id);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Message fetch timed out after {Timeout}s. Returning {Count} messages fetched so far.",
                MessageFetchTimeout.TotalSeconds, messages.Count);
        }

        return messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .OrderBy(m => m.Timestamp)
            .Take(count)
            .ToList();
    }

    private async Task<IEnumerable<IMessage>> FetchMessagesWithRetryAsync(
        SocketTextChannel channel,
        ulong? beforeMessageId,
        CancellationToken ct)
    {
        var delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return beforeMessageId == null
                    ? await channel.GetMessagesAsync(limit: 100).FlattenAsync()
                    : await channel.GetMessagesAsync(beforeMessageId.Value, Direction.Before, 100).FlattenAsync();
            }
            catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                if (attempt == MaxRetries)
                {
                    _logger.LogWarning("Discord API rate limited after {Attempts} attempts", MaxRetries);
                    throw;
                }

                _logger.LogDebug("Discord API rate limited, retrying in {Delay}ms (attempt {Attempt}/{MaxRetries})",
                    delay.TotalMilliseconds, attempt, MaxRetries);

                await Task.Delay(delay, ct);
                delay *= 2; // Exponential backoff
            }
        }

        return Enumerable.Empty<IMessage>();
    }
}

using Discord;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using TLDRkseid.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TLDRkseid.Commands;

public class TldrModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AiSummarizerService _summarizer;
    private readonly SummaryCacheService _cache;
    private readonly CostTrackerService _costTracker;
    private readonly SpamBlockerService _spamBlocker;
    private readonly GuildAccessService _access;
    private readonly GuildSettingsService _guildSettings;
    private readonly ILogger<TldrModule> _logger;

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
        GuildSettingsService guildSettings,
        SpamBlockerService spamBlocker,
        ILogger<TldrModule> logger)
    {
        _summarizer = summarizer;
        _cache = cache;
        _costTracker = costTracker;
        _access = access;
        _guildSettings = guildSettings;
        _spamBlocker = spamBlocker;
        _logger = logger;
    }

    [SlashCommand("tldr", "Summarize recent messages (uses server default if no depth specified)")]
    public async Task TldrAsync(
       [Summary(description: "Summary depth: recent, brief, standard, deep, or max (optional)")] string? depth = null,
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
            // Use guild's preferred depth if none specified
            var effectiveDepth = depth;
            var usedDefault = false;
            if (string.IsNullOrWhiteSpace(effectiveDepth))
            {
                if (Context.Guild != null)
                {
                    effectiveDepth = await _guildSettings.GetPreferredDepthAsync(Context.Guild.Id);
                    usedDefault = true;
                    _logger.LogInformation("Using guild's preferred depth: {Depth}", effectiveDepth);
                }
                else
                {
                    effectiveDepth = GuildSettingsService.DefaultDepth;
                    usedDefault = true;
                }
            }

            if (!DepthMap.TryGetValue(effectiveDepth, out var messageLimit))
            {
                _logger.LogWarning("Invalid depth parameter received: {Depth}", effectiveDepth);
                await RespondAsync("❌ Invalid depth. Try `/tldr-help` for valid options.", ephemeral: true);
                return;
            }

            // Support both text channels and thread/forum channels
            SocketGuild? guild = null;
            ISocketMessageChannel? messageChannel = null;
            SocketGuildChannel? guildChannel = null;

            if (Context.Channel is SocketTextChannel textChannel)
            {
                guild = textChannel.Guild;
                messageChannel = textChannel;
                guildChannel = textChannel;
            }
            else if (Context.Channel is SocketThreadChannel threadChannel)
            {
                guild = threadChannel.Guild;
                messageChannel = threadChannel;
                guildChannel = threadChannel;
                _logger.LogInformation("Command invoked in thread/forum: {ThreadName}", threadChannel.Name);
            }
            else
            {
                _logger.LogWarning("Command invoked in unsupported channel type: {ChannelType}", Context.Channel.GetType().Name);
                await RespondAsync("❌ This command only works in text channels, threads, or forum posts.", ephemeral: true);
                return;
            }

            var botUser = guild.GetUser(Context.Client.CurrentUser.Id);
            if (botUser == null)
            {
                _logger.LogError("Bot user was null in guild {GuildName}", guild.Name);
                await RespondAsync("⚠️ Could not verify bot permissions in this channel.", ephemeral: true);
                return;
            }

            var permissions = botUser.GetPermissions(guildChannel);
            var missingPerms = new List<string>();
            if (!permissions.ViewChannel) missingPerms.Add("View Channel");
            if (!permissions.ReadMessageHistory) missingPerms.Add("Read Message History");
            if (!permissions.SendMessages) missingPerms.Add("Send Messages");

            if (missingPerms.Count > 0)
            {
                _logger.LogWarning("Insufficient bot permissions in channel {ChannelName}: {MissingPerms}",
                    guildChannel.Name, string.Join(", ", missingPerms));
                await RespondAsync($"🚫 I'm missing these permissions in this channel:\n• {string.Join("\n• ", missingPerms)}\n\nPlease ask a server admin to grant these permissions.", ephemeral: true);
                return;
            }

            // Always defer first to show "thinking..." indicator
            await DeferAsync(ephemeral: true);

            try
            {
            // Warn about potential issues with "max" depth
            if (effectiveDepth == "max")
            {
                await FollowupAsync("⚠️ `max` depth may result in slower or overly broad summaries. Use `/tldr-help` for more focused tiers.", ephemeral: true);
            }

            _logger.LogInformation("Fetching up to {MessageLimit} messages for depth {Depth}.", messageLimit, effectiveDepth);

            var messages = await FetchRecentMessages(messageChannel!, messageLimit);
            _logger.LogInformation("Fetched {MessageCount} messages from channel.", messages.Count);

            var filtered = messages
                .Where(m => m.Author != null && !m.Author.IsBot)
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

            if (effectiveDepth == "max" && repetitionRatio < 0.6)
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

            _logger.LogInformation("Command invoked: /tldr depth:{Depth} user:{User}", effectiveDepth, user?.Username ?? "none");

            if (_cache.TryGet(guildIdStr, channelIdStr, effectiveDepth, userIdStr, filtered, out summary, out cost))
            {
                wasCached = true;
                _logger.LogInformation("Cache HIT for command /tldr with depth: {Depth} in channel: {ChannelId}", effectiveDepth, channelIdStr);

                if (_spamBlocker.IsCachedSpamming(guildIdStr, channelIdStr, Context.User.Id.ToString(), out var spamReason))
                {
                    _logger.LogWarning("Cached spam threshold exceeded: {Reason}", spamReason);
                    await FollowupAsync(spamReason, ephemeral: true);
                    return;
                }
            }
            else
            {
                _logger.LogInformation("Cache MISS for command /tldr with depth: {Depth} in channel: {ChannelId}", effectiveDepth, channelIdStr);

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
                    _cache.Set(guildIdStr, channelIdStr, effectiveDepth, userIdStr, filtered, summary, cost);

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
                           : effectiveDepth == "max" ? Color.DarkRed
                           : Color.DarkPurple;

            var titleSuffix = wasTruncated ? " (Truncated)" : wasCached ? " ⚡" : "";
            var depthLabel = usedDefault ? $"{effectiveDepth.ToUpper()} (default)" : effectiveDepth.ToUpper();

            var embed = new EmbedBuilder()
                .WithTitle($"TL;DRkseid Summary – {depthLabel}{titleSuffix}")
                .WithDescription(displaySummary)
                .WithColor(embedColor)
                .WithFooter(new EmbedFooterBuilder { Text = footerText });

            var builder = new ComponentBuilder()
                .WithButton("Buy Me a Coffee ☕", style: ButtonStyle.Link, url: "https://buymeacoffee.com/mcarthey");

            _logger.LogInformation("Sending summary response to user.");
            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during /tldr command execution");
                await FollowupAsync("❌ An unexpected error occurred while generating the summary. Please try again.", ephemeral: true);
            }
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
            // Get current guild default if in a guild
            var defaultDepth = Context.Guild != null
                ? await _guildSettings.GetPreferredDepthAsync(Context.Guild.Id)
                : GuildSettingsService.DefaultDepth;

            var embed = new EmbedBuilder()
                .WithTitle("🧠 TL;DRkseid Help")
                .WithDescription("AI-powered conversation summaries for Discord.")
                .WithColor(Color.Teal)
                .AddField("📋 Basic Usage",
                    "`/tldr` - Summarize using server default depth\n" +
                    "`/tldr depth:brief` - Summarize with specific depth\n" +
                    "`/tldr user:@someone` - Summarize one person's messages",
                    inline: false)
                .AddField("📊 Depth Levels",
                    $"**recent** (~100 msgs) - Quick skim\n" +
                    $"**brief** (~200 msgs) - Short break catch-up\n" +
                    $"**standard** (~300 msgs) - Daily check-in{(defaultDepth == "standard" ? " ⭐ (server default)" : "")}\n" +
                    $"**deep** (~400 msgs) - Extended absence{(defaultDepth == "deep" ? " ⭐ (server default)" : "")}\n" +
                    $"**max** (~500 msgs) - Full deep-dive ⚠️{(defaultDepth == "max" ? " (server default)" : "")}",
                    inline: false)
                .AddField("💡 Tips",
                    "• Summaries are private (only you see them)\n" +
                    "• Cached results show ⚡ and are instant & free\n" +
                    "• Green = cached, Purple = fresh, Orange = truncated",
                    inline: false)
                .AddField("🔧 Other Commands",
                    "`/cost` - View API usage statistics\n" +
                    "`/tldr-config` - (Admins) Set server default depth\n" +
                    "`$admin` - (Admins) Manage bot permissions",
                    inline: false)
                .WithFooter($"Server default: {defaultDepth} • github.com/mcarthey/Discord-TLDRkseid");

            _logger.LogInformation("Providing tldr-help response.");
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    [SlashCommand("cost", "View API usage statistics")]
    public async Task CostAsync()
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = Context.Guild?.Id ?? 0,
            ["GuildName"] = Context.Guild?.Name ?? "DM",
            ["InvokerId"] = Context.User.Id,
            ["InvokerName"] = Context.User.Username
        }))
        {
            var totalCost = _costTracker.GetTotal();

            var embed = new EmbedBuilder()
                .WithTitle("💰 TL;DRkseid Usage Statistics")
                .WithColor(Color.Gold)
                .AddField("Total API Cost", $"${totalCost:F4}", inline: true)
                .AddField("Model", "GPT-3.5-turbo", inline: true)
                .AddField("Rate", "$0.002 / 1K tokens", inline: true)
                .AddField("💡 Save Money",
                    "• Cached summaries are **free** (shown with ⚡)\n" +
                    "• Use lower depth levels when possible\n" +
                    "• Filter by user to reduce token count",
                    inline: false)
                .WithFooter("Cost data persisted to database • Per-guild tracking enabled");

            _logger.LogInformation("Providing cost statistics. Total: ${Total:F4}", totalCost);
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    [SlashCommand("tldr-config", "Configure TLDRkseid settings for this server (Admin only)")]
    public async Task TldrConfigAsync(
        [Summary(description: "Set default summary depth")] string? defaultDepth = null)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = Context.Guild?.Id ?? 0,
            ["GuildName"] = Context.Guild?.Name ?? "DM",
            ["InvokerId"] = Context.User.Id,
            ["InvokerName"] = Context.User.Username
        }))
        {
            if (Context.Guild == null)
            {
                await RespondAsync("❌ This command can only be used in a server.", ephemeral: true);
                return;
            }

            // Check if user is admin
            var isAdmin = await _access.CanAccessAdminFeaturesAsync(Context.Guild.Id, Context.User.Id);
            if (!isAdmin)
            {
                _logger.LogWarning("Non-admin user {UserId} attempted to use /tldr-config", Context.User.Id);
                await RespondAsync("⛔ Only server admins can configure TLDRkseid settings.", ephemeral: true);
                return;
            }

            // If no parameter provided, show current settings
            if (string.IsNullOrWhiteSpace(defaultDepth))
            {
                var currentDepth = await _guildSettings.GetPreferredDepthAsync(Context.Guild.Id);
                var embed = new EmbedBuilder()
                    .WithTitle("⚙️ TL;DRkseid Configuration")
                    .WithColor(Color.Blue)
                    .AddField("Default Depth", currentDepth, inline: true)
                    .AddField("Valid Options", "recent, brief, standard, deep, max", inline: false)
                    .AddField("Usage", "`/tldr-config defaultDepth:brief`", inline: false)
                    .WithFooter("Only admins can change these settings");

                await RespondAsync(embed: embed.Build(), ephemeral: true);
                return;
            }

            // Validate and set new depth
            if (!GuildSettingsService.IsValidDepth(defaultDepth))
            {
                await RespondAsync($"❌ Invalid depth `{defaultDepth}`. Valid options: recent, brief, standard, deep, max", ephemeral: true);
                return;
            }

            // Check rate limiting before database write
            if (_guildSettings.IsRateLimited(Context.Guild.Id, out var rateLimitReason))
            {
                _logger.LogWarning("Config change rate limited for guild {GuildId}: {Reason}", Context.Guild.Id, rateLimitReason);
                await RespondAsync(rateLimitReason, ephemeral: true);
                return;
            }

            await _guildSettings.SetPreferredDepthAsync(Context.Guild.Id, defaultDepth);
            _logger.LogInformation("Admin {UserId} set default depth to {Depth} for guild {GuildId}",
                Context.User.Id, defaultDepth, Context.Guild.Id);

            await RespondAsync($"✅ Server default depth set to **{defaultDepth}**. Users can now run `/tldr` without specifying a depth.", ephemeral: true);
        }
    }

    private async Task<List<IMessage>> FetchRecentMessages(ISocketMessageChannel channel, int count)
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
        ISocketMessageChannel channel,
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
            catch (ArgumentNullException ex)
            {
                // Discord.NET bug: some messages with components fail to deserialize
                // Log and continue with empty batch rather than crashing
                _logger.LogWarning(ex, "Discord.NET deserialization error fetching messages (likely a message with unsupported components). Skipping batch.");
                return Enumerable.Empty<IMessage>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Catch other unexpected errors during message fetch
                _logger.LogWarning(ex, "Unexpected error fetching messages. Skipping batch.");
                return Enumerable.Empty<IMessage>();
            }
        }

        return Enumerable.Empty<IMessage>();
    }
}

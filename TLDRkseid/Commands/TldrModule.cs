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

    private static readonly Dictionary<string, TimeSpan> SinceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "1h", TimeSpan.FromHours(1) },
        { "6h", TimeSpan.FromHours(6) },
        { "12h", TimeSpan.FromHours(12) },
        { "24h", TimeSpan.FromHours(24) },
        { "1d", TimeSpan.FromDays(1) },
        { "3d", TimeSpan.FromDays(3) },
        { "7d", TimeSpan.FromDays(7) }
    };

    // Timeout for message fetch operations
    private static readonly TimeSpan MessageFetchTimeout = TimeSpan.FromSeconds(15);

    // Discord embed description limit
    private const int EmbedDescriptionLimit = 4096;

    // Max messages to fetch when using time-based filtering
    private const int MaxTimeFetchMessages = 500;

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
       [Summary(description: "Summary depth: recent, brief, standard, deep, or max")] string? depth = null,
       [Summary(description: "Time window: 1h, 6h, 12h, 24h, 3d, 7d (overrides depth)")] string? since = null,
       [Summary(description: "Optional user to filter")] IUser? user = null)
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
            // Validate time-based filter if provided
            TimeSpan? sinceDuration = null;
            if (!string.IsNullOrWhiteSpace(since))
            {
                if (!SinceMap.TryGetValue(since, out var duration))
                {
                    await RespondAsync("❌ Invalid `since` value. Options: `1h`, `6h`, `12h`, `24h`, `3d`, `7d`", ephemeral: true);
                    return;
                }
                sinceDuration = duration;
            }

            // Use guild's preferred depth if none specified and no time filter
            var effectiveDepth = depth;
            var usedDefault = false;
            if (sinceDuration == null)
            {
                if (string.IsNullOrWhiteSpace(effectiveDepth))
                {
                    if (Context.Guild != null)
                    {
                        effectiveDepth = await _guildSettings.GetPreferredDepthAsync(Context.Guild.Id);
                        usedDefault = true;
                    }
                    else
                    {
                        effectiveDepth = GuildSettingsService.DefaultDepth;
                        usedDefault = true;
                    }
                }

                if (!DepthMap.TryGetValue(effectiveDepth, out _))
                {
                    await RespondAsync("❌ Invalid depth. Try `/tldr-help` for valid options.", ephemeral: true);
                    return;
                }
            }
            else
            {
                // When using time-based, default depth label for display
                effectiveDepth = "time";
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
            }
            else
            {
                await RespondAsync("❌ This command only works in text channels, threads, or forum posts.", ephemeral: true);
                return;
            }

            var botUser = guild.GetUser(Context.Client.CurrentUser.Id);
            if (botUser == null)
            {
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
                await RespondAsync($"🚫 I'm missing these permissions in this channel:\n• {string.Join("\n• ", missingPerms)}\n\nPlease ask a server admin to grant these permissions.", ephemeral: true);
                return;
            }

            // Always defer first to show "thinking..." indicator
            await DeferAsync(ephemeral: true);

            try
            {
                int messageLimit;
                DateTimeOffset? cutoffTime = null;

                if (sinceDuration != null)
                {
                    messageLimit = MaxTimeFetchMessages;
                    cutoffTime = DateTimeOffset.UtcNow - sinceDuration.Value;
                    _logger.LogInformation("Time-based fetch: since {Since} (cutoff: {Cutoff}), max {Limit} messages",
                        since, cutoffTime, messageLimit);
                }
                else
                {
                    messageLimit = DepthMap[effectiveDepth];

                    if (effectiveDepth == "max")
                    {
                        await FollowupAsync("⚠️ `max` depth may result in slower or overly broad summaries. Use `/tldr-help` for more focused tiers.", ephemeral: true);
                    }
                }

                var messages = await FetchRecentMessages(messageChannel!, messageLimit, cutoffTime);
                _logger.LogInformation("Fetched {MessageCount} messages from channel.", messages.Count);

                var filtered = messages
                    .Where(m => m.Author != null && !m.Author.IsBot)
                    .Where(m => user == null || m.Author.Id == user.Id)
                    .OrderBy(m => m.Timestamp)
                    .Select(m => $"{m.Author.Username}: {m.Content}")
                    .ToList();

                if (!filtered.Any())
                {
                    var noMsgText = sinceDuration != null
                        ? $"🕵️ No messages found in the last {since}."
                        : "🕵️ No messages found for that depth and filter.";
                    await FollowupAsync(noMsgText, ephemeral: true);
                    return;
                }

                var uniqueLines = filtered.Select(m => m.ToLowerInvariant()).Distinct().Count();
                var repetitionRatio = (double)uniqueLines / filtered.Count;

                if (effectiveDepth == "max" && repetitionRatio < 0.6)
                {
                    await FollowupAsync("⚠️ A large portion of messages appear repetitive. Summary may be diluted or vague.", ephemeral: true);
                }

                string summary;
                double cost = 0;
                bool wasCached = false;
                var guildIdStr = Context.Guild?.Id.ToString() ?? "dm";
                var channelIdStr = Context.Channel.Id.ToString();
                string? userIdStr = user?.Id.ToString();
                var cacheDepthKey = sinceDuration != null ? $"since-{since}" : effectiveDepth;

                _logger.LogInformation("Command invoked: /tldr depth:{Depth} since:{Since} user:{User}",
                    effectiveDepth, since ?? "none", user?.Username ?? "none");

                if (_cache.TryGet(guildIdStr, channelIdStr, cacheDepthKey, userIdStr, filtered, out summary, out cost))
                {
                    wasCached = true;

                    if (_spamBlocker.IsCachedSpamming(guildIdStr, channelIdStr, Context.User.Id.ToString(), out var spamReason))
                    {
                        await FollowupAsync(spamReason, ephemeral: true);
                        return;
                    }
                }
                else
                {
                    var requestingUserId = Context.User.Id;
                    var isAdmin = await _access.CanAccessAdminFeaturesAsync(Context.Guild?.Id ?? 0, requestingUserId);

                    if (_spamBlocker.IsSpamming(guildIdStr, channelIdStr, requestingUserId.ToString(), isAdmin, wasCached: false, out var spamReason))
                    {
                        await FollowupAsync(spamReason, ephemeral: true);
                        return;
                    }

                    try
                    {
                        var (generatedSummary, generatedCost) = await _summarizer.SummarizeAsync(filtered);
                        summary = generatedSummary;
                        cost = generatedCost;
                        _cache.Set(guildIdStr, channelIdStr, cacheDepthKey, userIdStr, filtered, summary, cost);
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
                    displaySummary = summary[..(EmbedDescriptionLimit - 50)] + "\n\n*... (truncated due to length)*";
                    wasTruncated = true;
                }

                var cacheStatus = wasCached ? "⚡ Cached (instant & free)" : "🆕 Fresh summary";

                var footerText = $"{cacheStatus} • {(user != null ? $"Filtered: {user.Username}" : "All users")} • " +
                                 $"{filtered.Count} msgs • Cost: ${cost:F4}";

                var embedColor = wasTruncated ? Color.Orange
                               : wasCached ? Color.Green
                               : effectiveDepth == "max" ? Color.DarkRed
                               : sinceDuration != null ? Color.Blue
                               : Color.DarkPurple;

                var titleSuffix = wasTruncated ? " (Truncated)" : wasCached ? " ⚡" : "";
                string titleLabel;
                if (sinceDuration != null)
                {
                    titleLabel = $"Last {since}";
                }
                else
                {
                    titleLabel = usedDefault ? $"{effectiveDepth.ToUpper()} (default)" : effectiveDepth.ToUpper();
                }

                var embed = new EmbedBuilder()
                    .WithTitle($"TL;DRkseid Summary – {titleLabel}{titleSuffix}")
                    .WithDescription(displaySummary)
                    .WithColor(embedColor)
                    .WithFooter(new EmbedFooterBuilder { Text = footerText });

                var builder = new ComponentBuilder()
                    .WithButton("Buy Me a Coffee ☕", style: ButtonStyle.Link, url: "https://buymeacoffee.com/mcarthey");

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
                    "`/tldr since:24h` - Summarize last 24 hours\n" +
                    "`/tldr user:@someone` - Summarize one person's messages",
                    inline: false)
                .AddField("📊 Depth Levels (by message count)",
                    $"**recent** (~100 msgs) - Quick skim\n" +
                    $"**brief** (~200 msgs) - Short break catch-up\n" +
                    $"**standard** (~300 msgs) - Daily check-in{(defaultDepth == "standard" ? " ⭐ default" : "")}\n" +
                    $"**deep** (~400 msgs) - Extended absence{(defaultDepth == "deep" ? " ⭐ default" : "")}\n" +
                    $"**max** (~500 msgs) - Full deep-dive ⚠️{(defaultDepth == "max" ? " ⭐ default" : "")}",
                    inline: false)
                .AddField("⏰ Time Filters (by time window)",
                    "`1h` - Last hour\n" +
                    "`6h` - Last 6 hours\n" +
                    "`12h` - Last 12 hours\n" +
                    "`24h` / `1d` - Last 24 hours\n" +
                    "`3d` - Last 3 days\n" +
                    "`7d` - Last week",
                    inline: false)
                .AddField("💡 Tips",
                    "• Summaries are private (only you see them)\n" +
                    "• Cached results show ⚡ and are instant & free\n" +
                    "• Use `since:` for precise time windows, `depth:` for message counts\n" +
                    "• Combine with `user:` to filter specific people",
                    inline: false)
                .AddField("🔧 Other Commands",
                    "`/cost` - View API usage statistics\n" +
                    "`/invite` - Get the bot invite link\n" +
                    "`/about` - Bot info and links\n" +
                    "`/tldr-config` - (Admins) Set server default depth\n" +
                    "`$admin` - (Admins) Manage bot permissions",
                    inline: false)
                .WithFooter($"Server default: {defaultDepth} • github.com/mcarthey/Discord-TLDRkseid");

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    [SlashCommand("cost", "View API usage statistics for this server")]
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
            if (Context.Guild == null)
            {
                var globalTotal = await _costTracker.GetTotalAsync();
                await RespondAsync($"💰 Global API cost: **${globalTotal:F4}**", ephemeral: true);
                return;
            }

            var stats = await _costTracker.GetGuildStatsAsync(Context.Guild.Id);

            var embed = new EmbedBuilder()
                .WithTitle($"💰 TL;DRkseid Usage – {Context.Guild.Name}")
                .WithColor(Color.Gold)
                .AddField("📊 This Server",
                    $"**Today:** ${stats.TodayCost:F4} ({stats.TodayRequests} requests)\n" +
                    $"**All Time:** ${stats.TotalCost:F4} ({stats.TotalRequests} requests)",
                    inline: false)
                .AddField("🔧 Model", "GPT-3.5-turbo ($0.002/1K tokens)", inline: true)
                .AddField("💡 Save Money",
                    "• Cached summaries are **free** (shown with ⚡)\n" +
                    "• Use `since:1h` for smaller windows\n" +
                    "• Filter by `user:` to reduce token count",
                    inline: false)
                .WithFooter("Cost data persisted to database • Updated in real-time");

            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    [SlashCommand("invite", "Get the link to add TLDRkseid to your server")]
    public async Task InviteAsync()
    {
        var embed = new EmbedBuilder()
            .WithTitle("🔗 Add TLDRkseid to Your Server")
            .WithDescription("[**Click here to invite TLDRkseid**](https://discord.com/api/oauth2/authorize?client_id=1360355381875970239&permissions=93184&scope=bot%20applications.commands)")
            .WithColor(Color.Blue)
            .AddField("Required Permissions",
                "• View Channels\n• Send Messages\n• Read Message History\n• Embed Links\n• Manage Messages (for admin commands)",
                inline: false)
            .WithFooter("TLDRkseid • AI-powered conversation summaries");

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    [SlashCommand("about", "Learn about TLDRkseid")]
    public async Task AboutAsync()
    {
        var guildCount = Context.Client.Guilds.Count;

        var embed = new EmbedBuilder()
            .WithTitle("🧠 About TL;DRkseid")
            .WithDescription(
                "**TLDRkseid** summarizes Discord conversations using AI so you never have to ask \"what did I miss?\"\n\n" +
                "Unlike other summary bots, TLDRkseid features smart caching (instant re-summaries at zero cost), " +
                "time-based filtering, per-user summaries, and tiered depth levels — all with ephemeral responses for privacy.")
            .WithColor(Color.Teal)
            .AddField("📈 Stats",
                $"Servers: **{guildCount}**\n" +
                $"Commands: `/tldr`, `/cost`, `/invite`, `/tldr-help`",
                inline: true)
            .AddField("🔗 Links",
                "[GitHub](https://github.com/mcarthey/Discord-TLDRkseid) • " +
                "[Privacy Policy](https://github.com/mcarthey/Discord-TLDRkseid/blob/main/PRIVACY.md) • " +
                "[Terms of Service](https://github.com/mcarthey/Discord-TLDRkseid/blob/main/TERMS-OF-SERVICE.md)",
                inline: true)
            .AddField("☕ Support Development",
                "[Buy Me a Coffee](https://buymeacoffee.com/mcarthey)",
                inline: true)
            .WithFooter("Built with Discord.Net + OpenAI • Apache 2.0 License");

        await RespondAsync(embed: embed.Build(), ephemeral: true);
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

            var isAdmin = await _access.CanAccessAdminFeaturesAsync(Context.Guild.Id, Context.User.Id);
            if (!isAdmin)
            {
                await RespondAsync("⛔ Only server admins can configure TLDRkseid settings.", ephemeral: true);
                return;
            }

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

            if (!GuildSettingsService.IsValidDepth(defaultDepth))
            {
                await RespondAsync($"❌ Invalid depth `{defaultDepth}`. Valid options: recent, brief, standard, deep, max", ephemeral: true);
                return;
            }

            if (_guildSettings.IsRateLimited(Context.Guild.Id, out var rateLimitReason))
            {
                await RespondAsync(rateLimitReason, ephemeral: true);
                return;
            }

            await _guildSettings.SetPreferredDepthAsync(Context.Guild.Id, defaultDepth);
            _logger.LogInformation("Admin {UserId} set default depth to {Depth} for guild {GuildId}",
                Context.User.Id, defaultDepth, Context.Guild.Id);

            await RespondAsync($"✅ Server default depth set to **{defaultDepth}**. Users can now run `/tldr` without specifying a depth.", ephemeral: true);
        }
    }

    private async Task<List<IMessage>> FetchRecentMessages(ISocketMessageChannel channel, int count, DateTimeOffset? cutoffTime = null)
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

                // If time-based filtering, stop when we pass the cutoff
                if (cutoffTime != null)
                {
                    var relevantBatch = batch.Where(m => m.Timestamp >= cutoffTime.Value).ToList();
                    messages.AddRange(relevantBatch);

                    // If we got fewer messages than the batch, we've passed the cutoff
                    if (relevantBatch.Count < batch.Count())
                        break;
                }
                else
                {
                    messages.AddRange(batch);
                }

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

                await Task.Delay(delay, ct);
                delay *= 2;
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(ex, "Discord.NET deserialization error fetching messages. Skipping batch.");
                return Enumerable.Empty<IMessage>();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Unexpected error fetching messages. Skipping batch.");
                return Enumerable.Empty<IMessage>();
            }
        }

        return Enumerable.Empty<IMessage>();
    }
}

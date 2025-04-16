using Discord;
using Discord.Interactions;
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
            if (!permissions.SendMessages || !permissions.ViewChannel)
            {
                _logger.LogWarning("Insufficient bot permissions in channel {ChannelName}.", textChannel.Name);
                await RespondAsync("🚫 I don’t have permission to post summaries in this channel.", ephemeral: true);
                return;
            }

            // Inform user about potential issues with "max" depth
            if (depth == "max")
            {
                await RespondAsync("⚠️ `max` depth may result in slower or overly broad summaries. Use `/tldr help` for more focused tiers.", ephemeral: true);
            }
            else
            {
                await DeferAsync(ephemeral: true);
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
            var channelIdStr = Context.Channel.Id.ToString();
            string? userIdStr = user == null ? null : user.Id.ToString();

            _logger.LogInformation("Command invoked: /tldr depth:{Depth} user:{User}", depth, user?.Username ?? "none");

            if (_cache.TryGet(channelIdStr, depth, userIdStr, filtered, out summary, out cost))
            {
                _logger.LogInformation("Cache HIT for command /tldr with depth: {Depth} in channel: {ChannelId}", depth, channelIdStr);

                if (_spamBlocker.IsCachedSpamming(Context.Guild?.Id.ToString() ?? "dm", channelIdStr, Context.User.Id.ToString(), out var spamReason))
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

                if (_spamBlocker.IsSpamming(Context.Guild?.Id.ToString() ?? "dm", channelIdStr, requestingUserId.ToString(), isAdmin, wasCached: false, out var spamReason))
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
                    _cache.Set(channelIdStr, depth, userIdStr, filtered, summary, cost);

                    _logger.LogInformation("Summarization succeeded with cost: {Cost:C}, summary length: {Length} characters.", cost, summary.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI summarization failed.");
                    await FollowupAsync("🤖 AI summarization failed. Try again later.", ephemeral: true);
                    return;
                }
            }

            var footerText = $"{(user != null ? $"Filtered by: {user.Username}" : "All users")} • " +
                             $"This summary cost: ${cost:F4} • Total spent: ${_costTracker.GetTotal():F2} • ☕";

            var embed = new EmbedBuilder()
                .WithTitle($"TL;DRkseid Summary – {depth.ToUpper()}")
                .WithDescription(summary)
                .WithColor(depth == "max" ? Color.DarkRed : Color.DarkPurple)
                .WithFooter(new EmbedFooterBuilder { Text = footerText });

            var builder = new ComponentBuilder()
                .WithButton("Buy Me a Coffee ☕", style: ButtonStyle.Link, url: "https://buymeacoffee.com/mcarthey");

            _logger.LogInformation("Sending summary response to user.");
            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }
    }

    [SlashCommand("tldr-help", "List depth options for TL;DR summaries")]
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
                .WithTitle("🧠 TL;DRkseid Summary Depths")
                .WithDescription("Choose a depth when running `/tldr`. Each level reaches further back in the conversation.")
                .WithColor(Color.Teal)
                .AddField("recent", "For a quick skim of the latest chatter.")
                .AddField("brief", "For catching up after a short break.")
                .AddField("standard", "⚙️ Recommended for regular check-ins.")
                .AddField("deep", "For catching up after an extended absence.")
                .AddField("max", "⚠️ Broad and deep. May result in diluted detail.")
                .WithFooter("Tip: Try `/tldr depth:standard` or `/tldr depth:brief user:@someone`");

            _logger.LogInformation("Providing tldr-help response.");
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }

    private async Task<List<IMessage>> FetchRecentMessages(SocketTextChannel channel, int count)
    {
        var messages = new List<IMessage>();
        ulong? beforeMessageId = null;

        while (messages.Count < count)
        {
            var batch = beforeMessageId == null
                ? await channel.GetMessagesAsync(limit: 100).FlattenAsync()
                : await channel.GetMessagesAsync(beforeMessageId.Value, Direction.Before, 100).FlattenAsync();

            if (!batch.Any()) break;

            messages.AddRange(batch);
            beforeMessageId = batch.Min(m => m.Id);
        }

        return messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .OrderBy(m => m.Timestamp)
            .Take(count)
            .ToList();
    }
}

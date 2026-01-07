using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using TLDRkseid.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TLDRkseid.Handlers;

public class MessageCommandHandler : IAsyncDisposable
{
    private readonly GuildAccessService _access;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;
    private readonly ILogger<MessageCommandHandler> _logger;
    private readonly SpamBlockerService _spamBlocker;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly ConcurrentDictionary<Guid, Task> _pendingDeletes = new();

    public MessageCommandHandler(
        GuildAccessService access,
        DiscordSocketClient client,
        InteractionService interactions,
        ILogger<MessageCommandHandler> logger,
        SpamBlockerService spamBlocker)
    {
        _access = access;
        _client = client;
        _interactions = interactions;
        _logger = logger;
        _spamBlocker = spamBlocker;
    }

    private void ScheduleDelete(IMessage msg, int delayMs = 10000)
    {
        var taskId = Guid.NewGuid();
        var task = DeleteAfterAsync(msg, delayMs, taskId, _shutdownCts.Token);
        _pendingDeletes.TryAdd(taskId, task);
    }

    private async Task DeleteAfterAsync(IMessage msg, int delayMs, Guid taskId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            await msg.DeleteAsync();
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested - expected, don't log as warning
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message {MessageId}", msg.Id);
        }
        finally
        {
            _pendingDeletes.TryRemove(taskId, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();

        // Wait for all pending deletes to complete (with timeout)
        var pendingTasks = _pendingDeletes.Values.ToArray();
        if (pendingTasks.Length > 0)
        {
            _logger.LogInformation("Waiting for {Count} pending message deletes to complete...", pendingTasks.Length);
            await Task.WhenAll(pendingTasks).WaitAsync(TimeSpan.FromSeconds(5));
        }

        _shutdownCts.Dispose();
    }

    public async Task HandleAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message) return;
        if (message.Author.IsBot || message.Channel is not SocketTextChannel channel) return;

        // Create a logging scope with context from the guild, channel and invoker
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["GuildId"] = channel.Guild.Id,
            ["GuildName"] = channel.Guild.Name,
            ["ChannelId"] = channel.Id,
            ["ChannelName"] = channel.Name,
            ["InvokerId"] = message.Author.Id,
            ["InvokerName"] = message.Author.Username
        }))
        {
            // Check if bot has permission to send messages in this channel
            var botUser = channel.Guild.GetUser(_client.CurrentUser.Id);
            if (botUser == null)
            {
                _logger.LogError("Could not retrieve bot user in guild {GuildName}", channel.Guild.Name);
                return;
            }

            var permissions = botUser.GetPermissions(channel);
            if (!permissions.SendMessages)
            {
                _logger.LogWarning("Bot lacks SendMessages permission in #{ChannelName}", channel.Name);
                return;
            }

            var content = message.Content.Trim();
            if (!content.StartsWith("!admin", System.StringComparison.OrdinalIgnoreCase)) return;

            // Rate limiting check for admin commands
            if (_spamBlocker.IsAdminCommandSpamming(channel.Guild.Id.ToString(), message.Author.Id.ToString(), out var spamReason))
            {
                _logger.LogWarning("Admin command rate limited for user {InvokerId}: {Reason}", message.Author.Id, spamReason);
                var rateLimitReply = await channel.SendMessageAsync($"{message.Author.Mention} {spamReason}");
                ScheduleDelete(message, 5000);
                ScheduleDelete(rateLimitReply, 5000);
                return;
            }

            // Log that an admin command has been received.
            _logger.LogInformation("Admin command received from user {InvokerName} ({InvokerId})", message.Author.Username, message.Author.Id);

            var guildId = channel.Guild.Id;
            var userId = message.Author.Id;
            var args = content.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

            // Delete the original message after 10s
            ScheduleDelete(message);

            // Helper local function to reply ephemerally and then delete the response
            async Task EphemeralReply(string text)
            {
                var reply = await channel.SendMessageAsync($"{message.Author.Mention} {text}");
                _logger.LogInformation("Sent ephemeral reply: {ReplyText}", text);
                ScheduleDelete(reply);
            }

            if (args.Length < 2)
            {
                _logger.LogWarning("Insufficient arguments for admin command.");
                await EphemeralReply("⚙️ Usage: `!admin add/remove/list/whoami/refresh/add-superuser`");
                return;
            }

            _logger.LogInformation("Processing admin command {Command} from message {MessageId} at {Timestamp}", args[1], message.Id, message.Timestamp);

            var command = args[1].ToLowerInvariant();

            // Check database rate limiting for write operations
            var isWriteCommand = command is "add-superuser" or "add" or "remove" or "transfer-superuser" or "revoke-superuser";
            if (isWriteCommand && _access.IsRateLimited(guildId, out var rateLimitReason))
            {
                _logger.LogWarning("Database write rate limited for guild {GuildId}: {Reason}", guildId, rateLimitReason);
                await EphemeralReply(rateLimitReason);
                return;
            }

            switch (command)
            {
                case "add-superuser":
                    // Security: Only guild owner can assign superuser
                    if (channel.Guild.OwnerId != userId)
                    {
                        _logger.LogWarning("Non-owner user {UserId} attempted to assign superuser in guild {GuildId}", userId, guildId);
                        await EphemeralReply("⛔ Only the server owner can assign a superuser.");
                        return;
                    }
                    if (await _access.GetSuperuserAsync(guildId) != null)
                    {
                        _logger.LogWarning("Superuser already assigned for guild {GuildId}", guildId);
                        await EphemeralReply("⚠️ A superuser is already assigned for this server.");
                        return;
                    }
                    var newSuperuser = message.MentionedUsers.FirstOrDefault();
                    if (args.Length < 3 || newSuperuser == null)
                    {
                        _logger.LogWarning("add-superuser command missing user mention.");
                        await EphemeralReply("⚠️ Tag a user: `!admin add-superuser @user`");
                        return;
                    }
                    var assigned = await _access.TryAssignSuperuserAsync(guildId, newSuperuser.Id, userId);
                    if (assigned)
                    {
                        await EphemeralReply($"👑 `{newSuperuser.Username}` is now the **superuser** for this server.");
                    }
                    else
                    {
                        _logger.LogError("Failed to assign superuser in guild {GuildId}", guildId);
                        await EphemeralReply("❌ Failed to assign superuser.");
                    }
                    break;

                case "add":
                case "remove":
                    if (!await _access.IsSuperuserAsync(guildId, userId))
                    {
                        _logger.LogWarning("User {UserId} is not superuser and cannot manage admins in guild {GuildId}", userId, guildId);
                        await EphemeralReply("⛔ Only the superuser can manage admins.");
                        return;
                    }
                    var targetUser = message.MentionedUsers.FirstOrDefault();
                    if (args.Length < 3 || targetUser == null)
                    {
                        _logger.LogWarning("Missing user mention in admin {Command} command.", command);
                        await EphemeralReply($"⚠️ Tag a user: `!admin {command} @user`");
                        return;
                    }
                    var action = command == "add"
                        ? await _access.AddAdminAsync(guildId, targetUser.Id, userId)
                        : await _access.RemoveAdminAsync(guildId, targetUser.Id, userId);
                    var verb = command == "add" ? "added as" : "removed from";
                    var alreadyText = command == "add" ? "already" : "not";
                    await EphemeralReply(action
                        ? $"✅ `{targetUser.Username}` was {verb} admin."
                        : $"⚠️ `{targetUser.Username}` was {alreadyText} an admin.");
                    break;

                case "list":
                    if (!await _access.IsSuperuserAsync(guildId, userId))
                    {
                        _logger.LogWarning("Non-superuser tried to list admins in guild {GuildId}", guildId);
                        await EphemeralReply("⛔ Only the superuser can list admins.");
                        return;
                    }
                    var admins = await _access.GetAdminsAsync(guildId);
                    if (!admins.Any())
                    {
                        _logger.LogInformation("No admins assigned in guild {GuildId}", guildId);
                        await EphemeralReply("📭 No admins assigned.");
                        return;
                    }
                    var names = admins.Select(id =>
                    {
                        var user = _client.GetUser(id);
                        return user != null ? $"• {user.Username}#{user.Discriminator}" : $"• Unknown User ({id})";
                    });
                    _logger.LogInformation("Listing admins for guild {GuildId}: {Admins}", guildId, string.Join(", ", names));
                    await EphemeralReply("🛡️ Admins:\n" + string.Join("\n", names));
                    break;

                case "whoami":
                    _logger.LogInformation("Processing 'whoami' command for user {UserId}", userId);
                    if (await _access.IsSuperuserAsync(guildId, userId))
                        await EphemeralReply("👑 You are the **superuser**.");
                    else if (await _access.IsAdminAsync(guildId, userId))
                        await EphemeralReply("🛡️ You are an **admin**.");
                    else
                        await EphemeralReply("👤 You are a regular user.");
                    break;

                case "refresh":
                    if (!await _access.CanAccessAdminFeaturesAsync(guildId, userId))
                    {
                        _logger.LogWarning("User {UserId} is not authorized to refresh commands in guild {GuildId}", userId, guildId);
                        await EphemeralReply("⛔ You are not authorized to refresh commands.");
                        return;
                    }
                    _logger.LogInformation("Refreshing commands for guild {GuildId}", guildId);
                    var app = await _client.GetApplicationInfoAsync();
                    // Delete GUILD commands
                    var guildCommands = await _client.Rest.GetGuildApplicationCommands(guildId);
                    foreach (var cmd in guildCommands)
                    {
                        await cmd.DeleteAsync();
                        _logger.LogInformation("Deleted GUILD command {CommandName} ({CommandId}) from guild {GuildId}", cmd.Name, cmd.Id, guildId);
                    }
                    // Delete GLOBAL commands
                    var globalCommands = await _client.Rest.GetGlobalApplicationCommands();
                    foreach (var cmd in globalCommands)
                    {
                        await cmd.DeleteAsync();
                        _logger.LogInformation("Deleted GLOBAL command {CommandName} ({CommandId})", cmd.Name, cmd.Id);
                    }
                    await _interactions.RegisterCommandsToGuildAsync(guildId, true);
                    await EphemeralReply("✅ All slash commands (guild + global) cleaned and refreshed for this server.");
                    break;

                case "transfer-superuser":
                    // Only server owner can transfer superuser
                    if (channel.Guild.OwnerId != userId)
                    {
                        _logger.LogWarning("Non-owner user {UserId} attempted to transfer superuser in guild {GuildId}", userId, guildId);
                        await EphemeralReply("⛔ Only the server owner can transfer superuser.");
                        return;
                    }
                    var transferTarget = message.MentionedUsers.FirstOrDefault();
                    if (args.Length < 3 || transferTarget == null)
                    {
                        _logger.LogWarning("transfer-superuser command missing user mention.");
                        await EphemeralReply("⚠️ Tag a user: `!admin transfer-superuser @user`");
                        return;
                    }
                    var transferred = await _access.TransferSuperuserAsync(guildId, transferTarget.Id, userId);
                    if (transferred)
                    {
                        await EphemeralReply($"👑 Superuser transferred to `{transferTarget.Username}`.");
                    }
                    else
                    {
                        await EphemeralReply("❌ No superuser to transfer. Use `!admin add-superuser @user` first.");
                    }
                    break;

                case "revoke-superuser":
                    // Only server owner can revoke superuser
                    if (channel.Guild.OwnerId != userId)
                    {
                        _logger.LogWarning("Non-owner user {UserId} attempted to revoke superuser in guild {GuildId}", userId, guildId);
                        await EphemeralReply("⛔ Only the server owner can revoke superuser.");
                        return;
                    }
                    var revoked = await _access.RevokeSuperuserAsync(guildId, userId);
                    if (revoked)
                    {
                        await EphemeralReply("✅ Superuser has been revoked. Use `!admin add-superuser @user` to assign a new one.");
                    }
                    else
                    {
                        await EphemeralReply("⚠️ No superuser is currently assigned.");
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown admin command received: {Command}", command);
                    await EphemeralReply("❓ Unknown subcommand. Try `add`, `remove`, `list`, `whoami`, `refresh`, `add-superuser`, `transfer-superuser`, or `revoke-superuser`.");
                    break;
            }
        }
    }
}

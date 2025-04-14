using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordPA.Services;

namespace DiscordPA.Handlers;

public class MessageCommandHandler
{
    private readonly GuildAccessService _access;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;

    public MessageCommandHandler(GuildAccessService access, DiscordSocketClient client, InteractionService interactions)
    {
        _access = access;
        _client = client;
        _interactions = interactions;
    }

    private async Task DeleteAfterAsync(IMessage msg, int delayMs = 10000)
    {
        await Task.Delay(delayMs);
        try { await msg.DeleteAsync(); } catch { /* ignored */ }
    }

    public async Task HandleAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message) return;
        if (message.Author.IsBot || message.Channel is not SocketTextChannel channel) return;

        var content = message.Content.Trim();
        if (!content.StartsWith("!admin", StringComparison.OrdinalIgnoreCase)) return;

        var guildId = channel.Guild.Id;
        var userId = message.Author.Id;
        var args = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Delete the original message after 10s
        _ = DeleteAfterAsync(message);

        // Delete the response after 10s
        async Task EphemeralReply(string text)
        {
            var reply = await channel.SendMessageAsync($"{message.Author.Mention} {text}");
            _ = DeleteAfterAsync(reply);
        }

        if (args.Length < 2)
        {
            await EphemeralReply("⚙️ Usage: `!admin add/remove/list/whoami/refresh/add-superuser`");
            return;
        }

        var command = args[1].ToLowerInvariant();

        switch (command)
        {
            case "add-superuser":
                if (await _access.GetSuperuserAsync(guildId) != null)
                {
                    await EphemeralReply("⚠️ A superuser is already assigned for this server.");
                    return;
                }

                if (args.Length < 3 || message.MentionedUsers.Count == 0)
                {
                    await EphemeralReply("⚠️ Tag a user: `!admin add-superuser @user`");
                    return;
                }

                var newSuperuser = message.MentionedUsers.First();
                var assigned = await _access.TryAssignSuperuserAsync(guildId, newSuperuser.Id);

                if (assigned)
                    await EphemeralReply($"👑 `{newSuperuser.Username}` is now the **superuser** for this server.");
                else
                    await EphemeralReply("❌ Failed to assign superuser.");
                break;

            case "add":
            case "remove":
                if (!await _access.IsSuperuserAsync(guildId, userId))
                {
                    await EphemeralReply("⛔ Only the superuser can manage admins.");
                    return;
                }

                if (args.Length < 3 || message.MentionedUsers.Count == 0)
                {
                    await EphemeralReply($"⚠️ Tag a user: `!admin {command} @user`");
                    return;
                }

                var targetUser = message.MentionedUsers.First();
                var action = command == "add"
                    ? await _access.AddAdminAsync(guildId, targetUser.Id)
                    : await _access.RemoveAdminAsync(guildId, targetUser.Id);

                var verb = command == "add" ? "added as" : "removed from";
                var alreadyText = command == "add" ? "already" : "not";

                await EphemeralReply(action
                    ? $"✅ `{targetUser.Username}` was {verb} admin."
                    : $"⚠️ `{targetUser.Username}` was {alreadyText} an admin.");
                break;

            case "list":
                if (!await _access.IsSuperuserAsync(guildId, userId))
                {
                    await EphemeralReply("⛔ Only the superuser can list admins.");
                    return;
                }

                var admins = await _access.GetAdminsAsync(guildId);
                if (!admins.Any())
                {
                    await EphemeralReply("📭 No admins assigned.");
                    return;
                }

                var names = admins.Select(id =>
                {
                    var user = _client.GetUser(id);
                    return user != null ? $"• {user.Username}#{user.Discriminator}" : $"• Unknown User ({id})";
                });

                await EphemeralReply("🛡️ Admins:\n" + string.Join("\n", names));
                break;

            case "whoami":
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
                    await EphemeralReply("⛔ You are not authorized to refresh commands.");
                    return;
                }

                await _interactions.RegisterCommandsToGuildAsync(guildId, true);
                await EphemeralReply("✅ Slash commands refreshed for this server.");
                break;

            default:
                await EphemeralReply("❓ Unknown subcommand. Try `add`, `remove`, `list`, `whoami`, `refresh`, or `add-superuser`.");
                break;
        }
    }
}

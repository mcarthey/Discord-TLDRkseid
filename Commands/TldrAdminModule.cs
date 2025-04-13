using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordPA.Services;

namespace DiscordPA.Commands;

[Group("tldr-admin", "Administrative commands for TLDrkseid")]
public class TldrAdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly SuperuserService _superuserService;
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactions;

    public TldrAdminModule(SuperuserService superuserService, DiscordSocketClient client, InteractionService interactions)
    {
        _superuserService = superuserService;
        _client = client;
        _interactions = interactions;
    }

    private ulong GuildId => Context.Guild?.Id ?? 0;

    [SlashCommand("add-superuser", "Assign the initial superuser for this server (only available once)")]
    public async Task AddSuperuserAsync(IUser user)
    {
        if (_superuserService.HasSuperuser(GuildId))
        {
            await RespondAsync("⚠️ A superuser is already assigned for this server.", ephemeral: true);
            return;
        }

        if (_superuserService.TryAssignSuperuser(GuildId, user.Id))
        {
            await RespondAsync($"✅ `{user.Username}` is now the superuser for this server.", ephemeral: true);
        }
        else
        {
            await RespondAsync("❌ Could not assign superuser.", ephemeral: true);
        }
    }

    [SlashCommand("add-admin", "Grant admin rights to a user in this server")]
    public async Task AddAdminAsync(IUser user)
    {
        if (!_superuserService.IsSuperuser(GuildId, Context.User.Id))
        {
            await RespondAsync("⛔ Only the superuser can add admins.", ephemeral: true);
            return;
        }

        if (_superuserService.AddAdmin(GuildId, user.Id))
        {
            await RespondAsync($"✅ `{user.Username}` is now an admin.", ephemeral: true);
        }
        else
        {
            await RespondAsync("⚠️ That user is already an admin.", ephemeral: true);
        }
    }

    [SlashCommand("remove-admin", "Revoke admin rights from a user in this server")]
    public async Task RemoveAdminAsync(IUser user)
    {
        if (!_superuserService.IsSuperuser(GuildId, Context.User.Id))
        {
            await RespondAsync("⛔ Only the superuser can remove admins.", ephemeral: true);
            return;
        }

        if (_superuserService.RemoveAdmin(GuildId, user.Id))
        {
            await RespondAsync($"✅ `{user.Username}` is no longer an admin.", ephemeral: true);
        }
        else
        {
            await RespondAsync("⚠️ That user was not an admin.", ephemeral: true);
        }
    }

    [SlashCommand("list-admins", "List all admins in this server")]
    public async Task ListAdminsAsync()
    {
        if (!_superuserService.IsSuperuser(GuildId, Context.User.Id))
        {
            await RespondAsync("⛔ Only the superuser can list admins.", ephemeral: true);
            return;
        }

        var admins = _superuserService.GetAdmins(GuildId);
        if (!admins.Any())
        {
            await RespondAsync("There are no admins assigned in this server.", ephemeral: true);
            return;
        }

        var names = admins.Select(id =>
        {
            var user = _client.GetUser(id);
            return user != null ? $"{user.Username}#{user.Discriminator}" : $"Unknown User ({id})";
        });

        await RespondAsync("🛡️ Admins:\n" + string.Join("\n", names), ephemeral: true);
    }

    [SlashCommand("whoami", "Check your current access level in this server")]
    public async Task WhoAmIAsync()
    {
        if (_superuserService.IsSuperuser(GuildId, Context.User.Id))
            await RespondAsync("👑 You are the **superuser** of this server.", ephemeral: true);
        else if (_superuserService.IsAdmin(GuildId, Context.User.Id))
            await RespondAsync("🛡️ You are an **admin** in this server.", ephemeral: true);
        else
            await RespondAsync("👤 You are a regular user.", ephemeral: true);
    }

    [SlashCommand("refresh-commands", "Force refresh slash commands in this guild")]
    public async Task RefreshCommandsAsync()
    {
        if (!_superuserService.CanAccessAdminFeatures(GuildId, Context.User.Id))
        {
            await RespondAsync("⛔ You are not authorized to use this command.", ephemeral: true);
            return;
        }

        await _interactions.RegisterCommandsToGuildAsync(GuildId, true);
        await RespondAsync("✅ Slash commands have been refreshed for this server.", ephemeral: true);
    }
}

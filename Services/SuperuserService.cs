using System.Text.Json;

namespace DiscordPA.Services;

public class SuperuserService
{
    private readonly string _filePath = Path.Combine(AppContext.BaseDirectory, "superuser.json");

    private readonly Dictionary<ulong, GuildAdminData> _guilds = new();

    public SuperuserService()
    {
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;

        var json = File.ReadAllText(_filePath);
        var data = JsonSerializer.Deserialize<SuperuserStore>(json);

        if (data?.Guilds != null)
        {
            foreach (var (guildId, info) in data.Guilds)
                _guilds[guildId] = info;
        }
    }

    private void Save()
    {
        var data = new SuperuserStore { Guilds = _guilds };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public bool HasSuperuser(ulong guildId) => _guilds.TryGetValue(guildId, out var data) && data.SuperuserId.HasValue;

    public bool IsSuperuser(ulong guildId, ulong userId) =>
        _guilds.TryGetValue(guildId, out var data) && data.SuperuserId == userId;

    public bool IsAdmin(ulong guildId, ulong userId) =>
        _guilds.TryGetValue(guildId, out var data) && data.AdminIds.Contains(userId);

    public bool CanAccessAdminFeatures(ulong guildId, ulong userId) =>
        IsSuperuser(guildId, userId) || IsAdmin(guildId, userId);

    public bool TryAssignSuperuser(ulong guildId, ulong userId)
    {
        if (_guilds.ContainsKey(guildId) && _guilds[guildId].SuperuserId.HasValue)
            return false;

        _guilds[guildId] = new GuildAdminData
        {
            SuperuserId = userId,
            AdminIds = new HashSet<ulong>()
        };

        Save();
        return true;
    }

    public bool AddAdmin(ulong guildId, ulong userId)
    {
        if (!_guilds.TryGetValue(guildId, out var data)) return false;

        var added = data.AdminIds.Add(userId);
        Save();
        return added;
    }

    public bool RemoveAdmin(ulong guildId, ulong userId)
    {
        if (!_guilds.TryGetValue(guildId, out var data)) return false;

        var removed = data.AdminIds.Remove(userId);
        Save();
        return removed;
    }

    public List<ulong> GetAdmins(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var data)) return new List<ulong>();
        return data.AdminIds.ToList();
    }

    public ulong? GetSuperuser(ulong guildId) =>
        _guilds.TryGetValue(guildId, out var data) ? data.SuperuserId : null;

    private class SuperuserStore
    {
        public Dictionary<ulong, GuildAdminData> Guilds { get; set; } = new();
    }

    private class GuildAdminData
    {
        public ulong? SuperuserId { get; set; }
        public HashSet<ulong> AdminIds { get; set; } = new();
    }
}

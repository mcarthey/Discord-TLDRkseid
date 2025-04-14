using DiscordPA.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordPA.Services;

public class GuildAccessService
{
    private readonly TldrDbContext _db;

    public GuildAccessService(TldrDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsSuperuserAsync(ulong guildId, ulong userId)
    {
        return await _db.GuildSuperusers.AnyAsync(x => x.GuildId == guildId && x.SuperuserId == userId);
    }

    public async Task<bool> IsAdminAsync(ulong guildId, ulong userId)
    {
        return await _db.GuildAdmins.AnyAsync(x => x.GuildId == guildId && x.UserId == userId);
    }

    public async Task<bool> CanAccessAdminFeaturesAsync(ulong guildId, ulong userId)
    {
        return await IsSuperuserAsync(guildId, userId) || await IsAdminAsync(guildId, userId);
    }

    public async Task<bool> TryAssignSuperuserAsync(ulong guildId, ulong userId)
    {
        if (await _db.GuildSuperusers.AnyAsync(x => x.GuildId == guildId))
            return false;

        _db.GuildSuperusers.Add(new GuildSuperuser
        {
            GuildId = guildId,
            SuperuserId = userId
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddAdminAsync(ulong guildId, ulong userId)
    {
        var exists = await _db.GuildAdmins.AnyAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (exists) return false;

        _db.GuildAdmins.Add(new GuildAdmin { GuildId = guildId, UserId = userId });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAdminAsync(ulong guildId, ulong userId)
    {
        var record = await _db.GuildAdmins.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (record == null) return false;

        _db.GuildAdmins.Remove(record);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ulong>> GetAdminsAsync(ulong guildId)
    {
        return await _db.GuildAdmins
            .Where(x => x.GuildId == guildId)
            .Select(x => x.UserId)
            .ToListAsync();
    }

    public async Task<ulong?> GetSuperuserAsync(ulong guildId)
    {
        return await _db.GuildSuperusers
            .Where(x => x.GuildId == guildId)
            .Select(x => (ulong?)x.SuperuserId)
            .FirstOrDefaultAsync();
    }
}

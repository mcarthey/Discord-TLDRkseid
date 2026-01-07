using DiscordPA.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordPA.Services;

public class GuildAccessService
{
    private readonly IDbContextFactory<TldrDbContext> _dbFactory;

    public GuildAccessService(IDbContextFactory<TldrDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<bool> IsSuperuserAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GuildSuperusers.AnyAsync(x => x.GuildId == guildId && x.SuperuserId == userId);
    }

    public async Task<bool> IsAdminAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GuildAdmins.AnyAsync(x => x.GuildId == guildId && x.UserId == userId);
    }

    public async Task<bool> CanAccessAdminFeaturesAsync(ulong guildId, ulong userId)
    {
        return await IsSuperuserAsync(guildId, userId) || await IsAdminAsync(guildId, userId);
    }

    public async Task<bool> TryAssignSuperuserAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.GuildSuperusers.AnyAsync(x => x.GuildId == guildId))
            return false;

        db.GuildSuperusers.Add(new GuildSuperuser
        {
            GuildId = guildId,
            SuperuserId = userId
        });

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddAdminAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.GuildAdmins.AnyAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (exists) return false;

        db.GuildAdmins.Add(new GuildAdmin { GuildId = guildId, UserId = userId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAdminAsync(ulong guildId, ulong userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var record = await db.GuildAdmins.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (record == null) return false;

        db.GuildAdmins.Remove(record);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ulong>> GetAdminsAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GuildAdmins
            .Where(x => x.GuildId == guildId)
            .Select(x => x.UserId)
            .ToListAsync();
    }

    public async Task<ulong?> GetSuperuserAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.GuildSuperusers
            .Where(x => x.GuildId == guildId)
            .Select(x => (ulong?)x.SuperuserId)
            .FirstOrDefaultAsync();
    }
}

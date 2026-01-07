using TLDRkseid.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TLDRkseid.Services;

public class GuildAccessService
{
    private readonly IDbContextFactory<TldrDbContext> _dbFactory;
    private readonly ILogger<GuildAccessService> _logger;
    private readonly SpamBlockerService _spamBlocker;

    public GuildAccessService(
        IDbContextFactory<TldrDbContext> dbFactory,
        ILogger<GuildAccessService> logger,
        SpamBlockerService spamBlocker)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _spamBlocker = spamBlocker;
    }

    public bool IsRateLimited(ulong guildId, out string reason)
    {
        return _spamBlocker.IsDatabaseWriteRateLimited(guildId, out reason);
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

    public async Task<bool> TryAssignSuperuserAsync(ulong guildId, ulong userId, ulong? actorId = null)
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

        _logger.LogInformation(
            "[AUDIT] Superuser assigned: Guild={GuildId}, NewSuperuser={UserId}, AssignedBy={ActorId}",
            guildId, userId, actorId ?? 0);

        return true;
    }

    public async Task<bool> TransferSuperuserAsync(ulong guildId, ulong newSuperuserId, ulong actorId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.GuildSuperusers.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (existing == null)
            return false;

        var oldSuperuserId = existing.SuperuserId;
        existing.SuperuserId = newSuperuserId;
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[AUDIT] Superuser transferred: Guild={GuildId}, OldSuperuser={OldId}, NewSuperuser={NewId}, TransferredBy={ActorId}",
            guildId, oldSuperuserId, newSuperuserId, actorId);

        return true;
    }

    public async Task<bool> RevokeSuperuserAsync(ulong guildId, ulong actorId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.GuildSuperusers.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (existing == null)
            return false;

        var oldSuperuserId = existing.SuperuserId;
        db.GuildSuperusers.Remove(existing);
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[AUDIT] Superuser revoked: Guild={GuildId}, RevokedSuperuser={UserId}, RevokedBy={ActorId}",
            guildId, oldSuperuserId, actorId);

        return true;
    }

    public async Task<bool> AddAdminAsync(ulong guildId, ulong userId, ulong? actorId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.GuildAdmins.AnyAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (exists) return false;

        db.GuildAdmins.Add(new GuildAdmin { GuildId = guildId, UserId = userId });
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[AUDIT] Admin added: Guild={GuildId}, NewAdmin={UserId}, AddedBy={ActorId}",
            guildId, userId, actorId ?? 0);

        return true;
    }

    public async Task<bool> RemoveAdminAsync(ulong guildId, ulong userId, ulong? actorId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var record = await db.GuildAdmins.FirstOrDefaultAsync(x => x.GuildId == guildId && x.UserId == userId);
        if (record == null) return false;

        db.GuildAdmins.Remove(record);
        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[AUDIT] Admin removed: Guild={GuildId}, RemovedAdmin={UserId}, RemovedBy={ActorId}",
            guildId, userId, actorId ?? 0);

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

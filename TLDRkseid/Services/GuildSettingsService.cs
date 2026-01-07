using TLDRkseid.Data;
using Microsoft.EntityFrameworkCore;

namespace TLDRkseid.Services;

public class GuildSettingsService
{
    private readonly IDbContextFactory<TldrDbContext> _dbFactory;
    private readonly SpamBlockerService _spamBlocker;

    // Valid depth options
    private static readonly HashSet<string> ValidDepths = new(StringComparer.OrdinalIgnoreCase)
    {
        "recent", "brief", "standard", "deep", "max"
    };

    public const string DefaultDepth = "standard";

    public GuildSettingsService(IDbContextFactory<TldrDbContext> dbFactory, SpamBlockerService spamBlocker)
    {
        _dbFactory = dbFactory;
        _spamBlocker = spamBlocker;
    }

    public bool IsRateLimited(ulong guildId, out string reason)
    {
        return _spamBlocker.IsDatabaseWriteRateLimited(guildId, out reason);
    }

    public async Task<GuildSettings> GetOrCreateAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.GuildSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new GuildSettings
            {
                GuildId = guildId,
                PreferredSummaryDepth = DefaultDepth,
                AutoSummarizeEnabled = false
            };
            db.GuildSettings.Add(settings);
            await db.SaveChangesAsync();
        }

        return settings;
    }

    public async Task<string> GetPreferredDepthAsync(ulong guildId)
    {
        var settings = await GetOrCreateAsync(guildId);
        return settings.PreferredSummaryDepth ?? DefaultDepth;
    }

    public async Task<bool> SetPreferredDepthAsync(ulong guildId, string depth)
    {
        if (!ValidDepths.Contains(depth))
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.GuildSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new GuildSettings
            {
                GuildId = guildId,
                PreferredSummaryDepth = depth.ToLowerInvariant(),
                AutoSummarizeEnabled = false
            };
            db.GuildSettings.Add(settings);
        }
        else
        {
            settings.PreferredSummaryDepth = depth.ToLowerInvariant();
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> GetAutoSummarizeEnabledAsync(ulong guildId)
    {
        var settings = await GetOrCreateAsync(guildId);
        return settings.AutoSummarizeEnabled;
    }

    public async Task SetAutoSummarizeEnabledAsync(ulong guildId, bool enabled)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var settings = await db.GuildSettings.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (settings == null)
        {
            settings = new GuildSettings
            {
                GuildId = guildId,
                PreferredSummaryDepth = DefaultDepth,
                AutoSummarizeEnabled = enabled
            };
            db.GuildSettings.Add(settings);
        }
        else
        {
            settings.AutoSummarizeEnabled = enabled;
        }

        await db.SaveChangesAsync();
    }

    public static bool IsValidDepth(string depth) => ValidDepths.Contains(depth);
}

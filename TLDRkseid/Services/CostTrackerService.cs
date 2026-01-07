using TLDRkseid.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TLDRkseid.Services;

public class CostTrackerService
{
    private readonly IDbContextFactory<TldrDbContext> _dbFactory;
    private readonly ILogger<CostTrackerService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CostTrackerService(IDbContextFactory<TldrDbContext> dbFactory, ILogger<CostTrackerService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Add cost for a specific guild's API usage.
    /// </summary>
    public async Task AddAsync(ulong guildId, double cost, int tokens = 0)
    {
        if (cost <= 0) return;

        await _lock.WaitAsync();
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var today = DateTime.UtcNow.Date;

            var entry = await db.CostEntries
                .FirstOrDefaultAsync(c => c.GuildId == guildId && c.DateUtc == today);

            if (entry == null)
            {
                entry = new CostEntry
                {
                    GuildId = guildId,
                    DateUtc = today,
                    TotalCost = cost,
                    RequestCount = 1,
                    TotalTokens = tokens,
                    UpdatedUtc = DateTime.UtcNow
                };
                db.CostEntries.Add(entry);
            }
            else
            {
                entry.TotalCost += cost;
                entry.RequestCount++;
                entry.TotalTokens += tokens;
                entry.UpdatedUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            _logger.LogDebug("Added cost ${Cost:F6} for guild {GuildId}. Daily total: ${DailyTotal:F4}",
                cost, guildId, entry.TotalCost);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility - tracks as global cost (guildId = 0).
    /// </summary>
    public async Task AddAsync(double cost)
    {
        await AddAsync(0, cost);
    }

    /// <summary>
    /// Get total cost across all guilds, all time.
    /// </summary>
    public async Task<double> GetTotalAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CostEntries.SumAsync(c => c.TotalCost);
    }

    /// <summary>
    /// Synchronous version for non-async contexts (e.g., embed footer).
    /// </summary>
    public double GetTotal()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.CostEntries.Sum(c => c.TotalCost);
    }

    /// <summary>
    /// Get total cost for a specific guild, all time.
    /// </summary>
    public async Task<double> GetGuildTotalAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.CostEntries
            .Where(c => c.GuildId == guildId)
            .SumAsync(c => c.TotalCost);
    }

    /// <summary>
    /// Get cost for a specific guild today.
    /// </summary>
    public async Task<double> GetGuildTodayAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = DateTime.UtcNow.Date;
        return await db.CostEntries
            .Where(c => c.GuildId == guildId && c.DateUtc == today)
            .SumAsync(c => c.TotalCost);
    }

    /// <summary>
    /// Get request count for a specific guild today.
    /// </summary>
    public async Task<int> GetGuildRequestCountTodayAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = DateTime.UtcNow.Date;
        var entry = await db.CostEntries
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.DateUtc == today);
        return entry?.RequestCount ?? 0;
    }

    /// <summary>
    /// Get usage statistics for a guild.
    /// </summary>
    public async Task<GuildUsageStats> GetGuildStatsAsync(ulong guildId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = DateTime.UtcNow.Date;

        var allTimeData = await db.CostEntries
            .Where(c => c.GuildId == guildId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCost = g.Sum(c => c.TotalCost),
                TotalRequests = g.Sum(c => c.RequestCount),
                TotalTokens = g.Sum(c => c.TotalTokens)
            })
            .FirstOrDefaultAsync();

        var todayData = await db.CostEntries
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.DateUtc == today);

        return new GuildUsageStats
        {
            GuildId = guildId,
            TotalCost = allTimeData?.TotalCost ?? 0,
            TotalRequests = allTimeData?.TotalRequests ?? 0,
            TotalTokens = allTimeData?.TotalTokens ?? 0,
            TodayCost = todayData?.TotalCost ?? 0,
            TodayRequests = todayData?.RequestCount ?? 0,
            TodayTokens = todayData?.TotalTokens ?? 0
        };
    }

    /// <summary>
    /// Get global usage statistics (all guilds combined).
    /// </summary>
    public async Task<GlobalUsageStats> GetGlobalStatsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var today = DateTime.UtcNow.Date;

        var allTimeData = await db.CostEntries
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCost = g.Sum(c => c.TotalCost),
                TotalRequests = g.Sum(c => c.RequestCount),
                TotalTokens = g.Sum(c => c.TotalTokens),
                UniqueGuilds = g.Select(c => c.GuildId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        var todayData = await db.CostEntries
            .Where(c => c.DateUtc == today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCost = g.Sum(c => c.TotalCost),
                TotalRequests = g.Sum(c => c.RequestCount)
            })
            .FirstOrDefaultAsync();

        return new GlobalUsageStats
        {
            TotalCost = allTimeData?.TotalCost ?? 0,
            TotalRequests = allTimeData?.TotalRequests ?? 0,
            TotalTokens = allTimeData?.TotalTokens ?? 0,
            UniqueGuilds = allTimeData?.UniqueGuilds ?? 0,
            TodayCost = todayData?.TotalCost ?? 0,
            TodayRequests = todayData?.TotalRequests ?? 0
        };
    }
}

public class GuildUsageStats
{
    public ulong GuildId { get; set; }
    public double TotalCost { get; set; }
    public int TotalRequests { get; set; }
    public int TotalTokens { get; set; }
    public double TodayCost { get; set; }
    public int TodayRequests { get; set; }
    public int TodayTokens { get; set; }
}

public class GlobalUsageStats
{
    public double TotalCost { get; set; }
    public int TotalRequests { get; set; }
    public int TotalTokens { get; set; }
    public int UniqueGuilds { get; set; }
    public double TodayCost { get; set; }
    public int TodayRequests { get; set; }
}

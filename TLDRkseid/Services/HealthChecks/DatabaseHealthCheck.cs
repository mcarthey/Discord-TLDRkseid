using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TLDRkseid.Data;

namespace TLDRkseid.Services.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<TldrDbContext> _dbFactory;

    public DatabaseHealthCheck(IDbContextFactory<TldrDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Quick connectivity test
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Cannot connect to database");
            }

            // Get some basic stats
            var guildCount = await db.GuildSettings.CountAsync(cancellationToken);
            var costEntryCount = await db.CostEntries.CountAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "GuildSettingsCount", guildCount },
                { "CostEntriesCount", costEntryCount },
                { "Provider", "SQLite" }
            };

            return HealthCheckResult.Healthy(
                $"Database connected. Guilds: {guildCount}, CostEntries: {costEntryCount}",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database check failed",
                exception: ex);
        }
    }
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DiscordPA.Services;

public class SummaryCacheService
{
    private class CachedSummary
    {
        public string Depth { get; set; } = "";
        public string SummaryText { get; set; } = "";
        public string MessageHash { get; set; } = "";
        public double Cost { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastAccessedUtc { get; set; }
    }

    // Configuration
    private const int MaxCacheEntries = 1000;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, CachedSummary> _cache = new();
    private readonly object _cleanupLock = new();

    private static string HashMessages(List<string> messages)
    {
        using var sha = SHA256.Create();
        var joined = string.Join("\n", messages);
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash);
    }

    private static readonly Dictionary<string, int> DepthOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        { "recent", 1 },
        { "brief", 2 },
        { "standard", 3 },
        { "deep", 4 },
        { "max", 5 }
    };

    public bool TryGet(string guildId, string channelId, string depth, string? userId, List<string> messages, out string summary, out double cost)
    {
        summary = string.Empty;
        cost = 0;
        var hash = HashMessages(messages);
        var key = BuildKey(guildId, channelId, depth, userId);
        var now = DateTime.UtcNow;

        // Direct match
        if (_cache.TryGetValue(key, out var cached) && cached.MessageHash == hash)
        {
            // Check TTL
            if (now - cached.CreatedUtc > CacheTtl)
            {
                _cache.TryRemove(key, out _);
                return false;
            }

            cached.LastAccessedUtc = now;
            summary = cached.SummaryText;
            cost = cached.Cost;
            return true;
        }

        // Trickledown from deeper cached tiers
        foreach (var tier in DepthOrder.Where(t => DepthOrder[t.Key] > DepthOrder[depth]).OrderBy(t => t.Value))
        {
            var upKey = BuildKey(guildId, channelId, tier.Key, userId);
            if (_cache.TryGetValue(upKey, out var upCached) && upCached.MessageHash == hash)
            {
                // Check TTL
                if (now - upCached.CreatedUtc > CacheTtl)
                {
                    _cache.TryRemove(upKey, out _);
                    continue;
                }

                upCached.LastAccessedUtc = now;
                summary = $"🧠 Cached from `{tier.Key}` tier:\n\n{upCached.SummaryText}";
                cost = upCached.Cost;
                return true;
            }
        }

        return false;
    }

    public void Set(string guildId, string channelId, string depth, string? userId, List<string> messages, string summaryText, double cost)
    {
        var hash = HashMessages(messages);
        var key = BuildKey(guildId, channelId, depth, userId);
        var now = DateTime.UtcNow;

        _cache[key] = new CachedSummary
        {
            Depth = depth,
            SummaryText = summaryText,
            MessageHash = hash,
            Cost = cost,
            CreatedUtc = now,
            LastAccessedUtc = now
        };

        // Cleanup if over capacity
        if (_cache.Count > MaxCacheEntries)
        {
            CleanupCache();
        }
    }

    private void CleanupCache()
    {
        lock (_cleanupLock)
        {
            if (_cache.Count <= MaxCacheEntries) return;

            var now = DateTime.UtcNow;

            // Remove expired entries first
            var expiredKeys = _cache
                .Where(kvp => now - kvp.Value.CreatedUtc > CacheTtl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            // If still over capacity, remove least recently accessed
            if (_cache.Count > MaxCacheEntries)
            {
                var toRemove = _cache
                    .OrderBy(kvp => kvp.Value.LastAccessedUtc)
                    .Take(_cache.Count - MaxCacheEntries + 100) // Remove extra to avoid frequent cleanup
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in toRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }

    private static string BuildKey(string guildId, string channelId, string depth, string? userId) =>
        $"{guildId}-{channelId}-{depth.ToLowerInvariant()}-{userId ?? "all"}";
}

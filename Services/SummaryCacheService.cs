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
        public double Cost { get; set; } = 0;
    }

    // Key: channelId-depth[-userId]
    private readonly Dictionary<string, CachedSummary> _cache = new();

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

    public bool TryGet(string channelId, string depth, string? userId, List<string> messages, out string summary, out double cost)
    {
        summary = string.Empty;
        cost = 0;
        var hash = HashMessages(messages);
        var key = BuildKey(channelId, depth, userId);

        // Direct match
        if (_cache.TryGetValue(key, out var cached) && cached.MessageHash == hash)
        {
            summary = cached.SummaryText;
            cost = cached.Cost;
            return true;
        }

        Console.WriteLine($"[Cache] Comparing stored hash: {cached?.MessageHash} with input: {hash}");

        // Trickledown from deeper cached tiers
        foreach (var tier in DepthOrder.Where(t => DepthOrder[t.Key] > DepthOrder[depth]).OrderBy(t => t.Value))
        {
            var upKey = BuildKey(channelId, tier.Key, userId);
            if (_cache.TryGetValue(upKey, out var upCached) && upCached.MessageHash == hash)
            {
                summary = $"🧠 Cached from `{tier.Key}` tier:\n\n{upCached.SummaryText}";
                cost = upCached.Cost;
                return true;
            }
        }

        return false;
    }

    public void Set(string channelId, string depth, string? userId, List<string> messages, string summaryText, double cost)
    {
        var hash = HashMessages(messages);
        var key = BuildKey(channelId, depth, userId);

        Console.WriteLine($"[Cache] Built hash: {hash} for key: {key}");

        _cache[key] = new CachedSummary
        {
            Depth = depth,
            SummaryText = summaryText,
            MessageHash = hash,
            Cost = cost
        };
    }

    private static string BuildKey(string channelId, string depth, string? userId) =>
        $"{channelId}-{depth.ToLowerInvariant()}-{userId ?? "all"}";
}

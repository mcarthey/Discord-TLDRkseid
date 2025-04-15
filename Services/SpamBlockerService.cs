using System.Collections.Concurrent;

namespace DiscordPA.Services;

public class SpamBlockerService
{
    private readonly TimeSpan _cooldownDuration = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _burstWindow = TimeSpan.FromSeconds(10);
    private readonly int _burstThreshold = 3;

    // (guildId-channelId-userId) => last request timestamp
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestMap = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _recentRequestsMap = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastCachedReplyMap = new();

    public bool IsCachedSpamming(string guildId, string channelId, string userId, out string reason)
    {
        reason = string.Empty;
        var key = $"{guildId}-{channelId}-{userId}";
        var now = DateTime.UtcNow;

        if (_lastCachedReplyMap.TryGetValue(key, out var last) && (now - last) < TimeSpan.FromSeconds(10))
        {
            var retry = 10 - (now - last).TotalSeconds;
            reason = $"🕓 You're requesting cached summaries too fast. Try again in {retry:F0}s.";
            Console.WriteLine($"[SpamBlocker] Cached spam triggered for User:{userId} in Channel:{channelId} Guild:{guildId} | Retry in {retry:F0}s");
            return true;
        }

        _lastCachedReplyMap[key] = now;
        return false;
    }


    public bool IsSpamming(string guildId, string channelId, string userId, bool isAdmin, bool wasCached, out string reason)
    {
        var key = $"{guildId}-{channelId}-{userId}";
        reason = "";

        if (isAdmin || wasCached)
            return false;

        var now = DateTime.UtcNow;

        // Cooldown check
        if (_lastRequestMap.TryGetValue(key, out var lastTime) &&
            (now - lastTime) < _cooldownDuration)
        {
            var remaining = (_cooldownDuration - (now - lastTime)).TotalSeconds;
            reason = $"⏳ Slow down! Try again in {remaining:F0}s.";
            Console.WriteLine($"[SpamBlocker] Cooldown triggered for User:{userId} in Channel:{channelId} Guild:{guildId} | Retry in {remaining:F0}s");
            return true;
        }

        _lastRequestMap[key] = now;

        // Burst detection
        var queue = _recentRequestsMap.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && (now - queue.Peek()) > _burstWindow)
                queue.Dequeue();

            if (queue.Count >= _burstThreshold)
            {
                reason = "⚠️ Whoa there! You're sending requests too quickly.";
                Console.WriteLine($"[SpamBlocker] Burst limit hit for User:{userId} in Channel:{channelId} Guild:{guildId} | Count:{queue.Count} in {Math.Floor(_burstWindow.TotalSeconds)}s");
                return true;
            }
        }

        return false;
    }
}

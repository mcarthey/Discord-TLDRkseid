using System.Collections.Concurrent;

namespace TLDRkseid.Services;

public class SpamBlockerService : IDisposable
{
    private readonly TimeSpan _cooldownDuration = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _burstWindow = TimeSpan.FromSeconds(10);
    private readonly int _burstThreshold = 3;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EntryExpiration = TimeSpan.FromMinutes(10);

    // Admin command rate limiting
    private readonly TimeSpan _adminCooldown = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _adminBurstWindow = TimeSpan.FromSeconds(30);
    private readonly int _adminBurstThreshold = 10;

    // Database write rate limiting (per guild)
    private readonly TimeSpan _dbWriteCooldown = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _dbWriteBurstWindow = TimeSpan.FromMinutes(1);
    private readonly int _dbWriteBurstThreshold = 20;

    // (guildId-channelId-userId) => last request timestamp
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestMap = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _recentRequestsMap = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastCachedReplyMap = new();

    // Admin command tracking
    private readonly ConcurrentDictionary<string, DateTime> _lastAdminCommandMap = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _adminBurstMap = new();

    // Database write tracking (per guild)
    private readonly ConcurrentDictionary<string, DateTime> _lastDbWriteMap = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _dbWriteBurstMap = new();

    private readonly Timer _cleanupTimer;

    public SpamBlockerService()
    {
        _cleanupTimer = new Timer(
            _ => CleanupStaleEntries(),
            null,
            CleanupInterval,
            CleanupInterval);
    }

    public bool IsCachedSpamming(string guildId, string channelId, string userId, out string reason)
    {
        reason = string.Empty;
        var key = $"{guildId}-{channelId}-{userId}";
        var now = DateTime.UtcNow;

        if (_lastCachedReplyMap.TryGetValue(key, out var last) && (now - last) < TimeSpan.FromSeconds(10))
        {
            var retry = 10 - (now - last).TotalSeconds;
            reason = $"🕓 **Cooldown active** - Cached summaries have a 10s cooldown. Try again in {retry:F0}s.";
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

        // Cooldown check - minimum time between requests
        if (_lastRequestMap.TryGetValue(key, out var lastTime) &&
            (now - lastTime) < _cooldownDuration)
        {
            var remaining = (_cooldownDuration - (now - lastTime)).TotalSeconds;
            reason = $"⏳ **Cooldown active** - Fresh summaries require a {_cooldownDuration.TotalSeconds:F0}s cooldown between requests. Try again in {remaining:F0}s.";
            return true;
        }

        _lastRequestMap[key] = now;

        // Burst detection - too many requests in short window
        var queue = _recentRequestsMap.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && (now - queue.Peek()) > _burstWindow)
                queue.Dequeue();

            if (queue.Count >= _burstThreshold)
            {
                reason = $"⚠️ **Burst limit reached** - You've made {_burstThreshold} requests in {_burstWindow.TotalSeconds:F0}s. Please wait a moment before trying again.";
                return true;
            }
        }

        return false;
    }

    public bool IsAdminCommandSpamming(string guildId, string userId, out string reason)
    {
        reason = string.Empty;
        var key = $"admin-{guildId}-{userId}";
        var now = DateTime.UtcNow;

        // Cooldown check - minimum time between admin commands
        if (_lastAdminCommandMap.TryGetValue(key, out var lastTime) &&
            (now - lastTime) < _adminCooldown)
        {
            var remaining = (_adminCooldown - (now - lastTime)).TotalSeconds;
            reason = $"⏳ **Cooldown active** - Admin commands have a {_adminCooldown.TotalSeconds:F0}s cooldown. Try again in {remaining:F0}s.";
            return true;
        }

        _lastAdminCommandMap[key] = now;

        // Burst detection - too many admin commands in short window
        var queue = _adminBurstMap.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && (now - queue.Peek()) > _adminBurstWindow)
                queue.Dequeue();

            if (queue.Count > _adminBurstThreshold)
            {
                reason = $"⚠️ **Burst limit reached** - You've run {_adminBurstThreshold} admin commands in {_adminBurstWindow.TotalSeconds:F0}s. Please wait before trying again.";
                return true;
            }
        }

        return false;
    }

    public bool IsDatabaseWriteRateLimited(ulong guildId, out string reason)
    {
        reason = string.Empty;
        var key = $"dbwrite-{guildId}";
        var now = DateTime.UtcNow;

        // Cooldown check - minimum time between database writes
        if (_lastDbWriteMap.TryGetValue(key, out var lastTime) &&
            (now - lastTime) < _dbWriteCooldown)
        {
            var remaining = (_dbWriteCooldown - (now - lastTime)).TotalSeconds;
            reason = $"⏳ **Rate limited** - Database operations have a {_dbWriteCooldown.TotalSeconds:F0}s cooldown. Try again in {remaining:F1}s.";
            return true;
        }

        _lastDbWriteMap[key] = now;

        // Burst detection - too many database writes in window
        var queue = _dbWriteBurstMap.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (queue)
        {
            queue.Enqueue(now);
            while (queue.Count > 0 && (now - queue.Peek()) > _dbWriteBurstWindow)
                queue.Dequeue();

            if (queue.Count > _dbWriteBurstThreshold)
            {
                reason = $"⚠️ **Rate limited** - Too many database operations ({_dbWriteBurstThreshold}) in {_dbWriteBurstWindow.TotalMinutes:F0} minute(s). Please wait before trying again.";
                return true;
            }
        }

        return false;
    }

    private void CleanupStaleEntries()
    {
        var cutoff = DateTime.UtcNow - EntryExpiration;

        // Cleanup last request map
        var staleKeys = _lastRequestMap
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _lastRequestMap.TryRemove(key, out _);
        }

        // Cleanup cached reply map
        staleKeys = _lastCachedReplyMap
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _lastCachedReplyMap.TryRemove(key, out _);
        }

        // Cleanup burst queues - remove empty or stale queues
        var staleQueueKeys = _recentRequestsMap
            .Where(kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.Count == 0 ||
                           (kvp.Value.Count > 0 && kvp.Value.Peek() < cutoff);
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleQueueKeys)
        {
            _recentRequestsMap.TryRemove(key, out _);
        }

        // Cleanup admin command tracking
        staleKeys = _lastAdminCommandMap
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _lastAdminCommandMap.TryRemove(key, out _);
        }

        // Cleanup admin burst queues
        staleQueueKeys = _adminBurstMap
            .Where(kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.Count == 0 ||
                           (kvp.Value.Count > 0 && kvp.Value.Peek() < cutoff);
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleQueueKeys)
        {
            _adminBurstMap.TryRemove(key, out _);
        }

        // Cleanup database write tracking
        staleKeys = _lastDbWriteMap
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _lastDbWriteMap.TryRemove(key, out _);
        }

        // Cleanup database write burst queues
        staleQueueKeys = _dbWriteBurstMap
            .Where(kvp =>
            {
                lock (kvp.Value)
                {
                    return kvp.Value.Count == 0 ||
                           (kvp.Value.Count > 0 && kvp.Value.Peek() < cutoff);
                }
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleQueueKeys)
        {
            _dbWriteBurstMap.TryRemove(key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

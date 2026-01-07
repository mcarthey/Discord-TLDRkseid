using Discord.WebSocket;
using DiscordPA.Models;
using System.Collections.Concurrent;

namespace DiscordPA.Services;

public class MessageCollectorService
{
    private readonly ConcurrentQueue<TrackedMessage> _messages = new();
    private readonly object _cleanupLock = new();

    // Configuration
    private const int MaxMessages = 10000;
    private static readonly TimeSpan MaxMessageAge = TimeSpan.FromHours(24);

    public void Track(SocketMessage message)
    {
        _messages.Enqueue(new TrackedMessage
        {
            Author = message.Author.Username,
            Channel = message.Channel.Name,
            Content = message.Content,
            Timestamp = message.Timestamp.UtcDateTime,
            ThreadTopic = (message.Channel as SocketThreadChannel)?.Name
        });

        // Cleanup if over capacity
        if (_messages.Count > MaxMessages)
        {
            CleanupOldMessages();
        }
    }

    public List<TrackedMessage> GetAndClearMessages()
    {
        var result = new List<TrackedMessage>();
        while (_messages.TryDequeue(out var msg))
        {
            result.Add(msg);
        }
        return result;
    }

    public List<TrackedMessage> GetMessagesSince(DateTime sinceUtc)
    {
        return _messages
            .Where(m => m.Timestamp >= sinceUtc)
            .ToList();
    }

    private void CleanupOldMessages()
    {
        lock (_cleanupLock)
        {
            var cutoff = DateTime.UtcNow - MaxMessageAge;
            var toKeep = _messages
                .Where(m => m.Timestamp >= cutoff)
                .OrderByDescending(m => m.Timestamp)
                .Take(MaxMessages)
                .ToList();

            // Clear and re-add
            while (_messages.TryDequeue(out _)) { }
            foreach (var msg in toKeep.OrderBy(m => m.Timestamp))
            {
                _messages.Enqueue(msg);
            }
        }
    }
}

// MessageCollectorService.cs

using Discord.WebSocket;
using DiscordPA.Models;

namespace DiscordPA.Services;

public class MessageCollectorService
{
    private readonly List<TrackedMessage> _messages = new();

    public void Track(SocketMessage message)
    {
        _messages.Add(new TrackedMessage
        {
            Author = message.Author.Username,
            Channel = message.Channel.Name,
            Content = message.Content,
            Timestamp = message.Timestamp.UtcDateTime,
            ThreadTopic = (message.Channel as SocketThreadChannel)?.Name
        });
    }

    public List<TrackedMessage> GetAndClearMessages()
    {
        var copy = new List<TrackedMessage>(_messages);
        _messages.Clear();
        return copy;
    }

    public List<TrackedMessage> GetMessagesSince(DateTime sinceUtc)
    {
        return _messages
            .Where(m => m.Timestamp >= sinceUtc)
            .ToList();
    }

}

// SummaryLogger.cs

using System.Text;

namespace DiscordPA.Services;

public class SummaryLogger
{
    private readonly MessageCollectorService _collector;
    private readonly string _logDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");

    public SummaryLogger(MessageCollectorService collector)
    {
        _collector = collector;
        Directory.CreateDirectory(_logDir);
    }

    public async Task LogSummary(string interval)
    {
        var now = DateTime.UtcNow;
        var messages = _collector.GetAndClearMessages();
        if (!messages.Any()) return;

        var grouped = messages.GroupBy(m => m.Channel)
            .Select(channelGroup => new
            {
                Channel = channelGroup.Key,
                Threads = channelGroup.GroupBy(m => m.ThreadTopic ?? "General")
                    .Select(threadGroup => new
                    {
                        Thread = threadGroup.Key,
                        Users = threadGroup.GroupBy(m => m.Author)
                            .ToDictionary(g => g.Key, g => g.Select(m => m.Content).ToList())
                    }).ToList()
            }).ToList();

        var logFile = Path.Combine(_logDir, $"{interval}/{now:yyyy-MM-dd_HH-mm}-summary.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);

        var sb = new StringBuilder();
        foreach (var channel in grouped)
        {
            sb.AppendLine($"# Channel: {channel.Channel}");
            foreach (var thread in channel.Threads)
            {
                sb.AppendLine($"  ## Thread: {thread.Thread}");
                foreach (var user in thread.Users)
                {
                    sb.AppendLine($"    - {user.Key}:");
                    foreach (var msg in user.Value)
                    {
                        sb.AppendLine($"      • {msg}");
                    }
                }
            }
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(logFile, sb.ToString());
    }
}
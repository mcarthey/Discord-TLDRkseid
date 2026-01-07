// TrackedMessage.cs
namespace TLDRkseid.Models;

public class TrackedMessage
{
    public string Author { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string? ThreadTopic { get; set; }
}

namespace DiscordPA.Data;

public class GuildSettings
{
    public ulong GuildId { get; set; }
    public string? PreferredSummaryDepth { get; set; }
    public bool AutoSummarizeEnabled { get; set; }
}

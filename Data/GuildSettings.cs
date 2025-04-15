using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiscordPA.Data;

[Table("GuildSettings")]
[PrimaryKey(nameof(GuildId))]
public class GuildSettings
{
    public ulong GuildId { get; set; }

    public string? PreferredSummaryDepth { get; set; }

    public bool AutoSummarizeEnabled { get; set; }
}

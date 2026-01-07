using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordPA.Data;

[Table("CostEntries")]
public class CostEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The guild ID where the cost was incurred. 0 for global/system costs.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The date this cost entry is for (UTC, date only - no time component).
    /// Allows for daily aggregation and historical tracking.
    /// </summary>
    public DateTime DateUtc { get; set; }

    /// <summary>
    /// Total cost accumulated for this guild on this date.
    /// </summary>
    public double TotalCost { get; set; }

    /// <summary>
    /// Number of API calls made for this guild on this date.
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Total tokens used for this guild on this date.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// When this entry was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}

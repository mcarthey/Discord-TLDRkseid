using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DiscordPA.Data;

[Table("GuildSuperusers")]
[PrimaryKey(nameof(GuildId))]
public class GuildSuperuser
{
    public ulong GuildId { get; set; }

    public ulong SuperuserId { get; set; }
}

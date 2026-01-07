using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TLDRkseid.Data;

[Table("GuildAdmins")]
[PrimaryKey(nameof(GuildId), nameof(UserId))]
public class GuildAdmin
{
    public ulong GuildId { get; set; }

    public ulong UserId { get; set; }
}

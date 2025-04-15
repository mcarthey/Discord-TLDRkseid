using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DiscordPA.Data
{
    public class LogEntry
    {
        [Key]
        public int Id { get; set; }

        // Timestamp of the log event
        [Required]
        public DateTime Timestamp { get; set; }

        // Severity level (Trace, Debug, Info, Warn, Error, Fatal)
        [Required]
        public string Level { get; set; } = "";

        // Logger name or source (e.g., "TLDrkseid.TldrModule")
        [Required]
        public string Logger { get; set; } = "";

        // Message content
        [Required]
        public string Message { get; set; } = "";

        // Optional exception info
        public string? Exception { get; set; }

        // Optional Discord context (guild, user, channel)
        public string? GuildName { get; set; }
        public ulong? GuildId { get; set; }
        public string? ChannelName { get; set; }
        public ulong? ChannelId { get; set; }
        public string? Username { get; set; }
        public ulong? UserId { get; set; }
    }
}

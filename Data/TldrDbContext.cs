using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;

namespace DiscordPA.Data;

public class TldrDbContext : DbContext
{
    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();
    public DbSet<GuildAdmin> GuildAdmins => Set<GuildAdmin>();
    public DbSet<GuildSuperuser> GuildSuperusers => Set<GuildSuperuser>();
    public DbSet<LogEntry> LogEntries => Set<LogEntry>(); // Added for NLog database logging

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=tldr.sqlite");
    }
    }

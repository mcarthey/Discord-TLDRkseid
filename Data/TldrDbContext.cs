using Microsoft.EntityFrameworkCore;

namespace DiscordPA.Data;

public class TldrDbContext : DbContext
{
    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();
    public DbSet<GuildAdmin> GuildAdmins => Set<GuildAdmin>();
    public DbSet<GuildSuperuser> GuildSuperusers => Set<GuildSuperuser>();
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<CostEntry> CostEntries => Set<CostEntry>();

    public TldrDbContext(DbContextOptions<TldrDbContext> options) : base(options)
    {
    }

    // Parameterless constructor for migrations and direct instantiation
    public TldrDbContext()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite("Data Source=tldr.sqlite");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Index for efficient cost queries by guild and date
        modelBuilder.Entity<CostEntry>()
            .HasIndex(c => new { c.GuildId, c.DateUtc })
            .IsUnique();
    }
}

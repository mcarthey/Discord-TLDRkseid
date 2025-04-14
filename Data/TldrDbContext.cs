using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace DiscordPA.Data;

public class TldrDbContext : DbContext
{
    public DbSet<GuildSettings> GuildSettings => Set<GuildSettings>();
    public DbSet<GuildAdmin> GuildAdmins => Set<GuildAdmin>();
    public DbSet<GuildSuperuser> GuildSuperusers => Set<GuildSuperuser>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        //var dbPath = Path.Combine(AppContext.BaseDirectory, "tldr.sqlite");

        options.UseSqlite("Data Source=tldr.sqlite");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuildSettings>().HasKey(g => g.GuildId);
        modelBuilder.Entity<GuildSuperuser>().HasKey(s => s.GuildId);
        modelBuilder.Entity<GuildAdmin>().HasKey(a => new { a.GuildId, a.UserId });
    }
}

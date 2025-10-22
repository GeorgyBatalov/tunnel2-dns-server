using Microsoft.EntityFrameworkCore;

namespace Tunnel2.DnsServer.Data;

/// <summary>
/// Database context for Tunnel2 DNS Server.
/// </summary>
public sealed class DnsServerDbContext : DbContext
{
    public DnsServerDbContext(DbContextOptions<DnsServerDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Active tunnel sessions table.
    /// </summary>
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Session entity
        modelBuilder.Entity<Session>(entity =>
        {
            // Index on hostname for fast DNS lookups
            entity.HasIndex(e => e.Hostname);

            // Index on expires_at for efficient cleanup by background worker
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}

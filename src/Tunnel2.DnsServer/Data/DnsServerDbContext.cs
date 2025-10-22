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
    /// Proxy entries table.
    /// </summary>
    public DbSet<ProxyEntry> ProxyEntries => Set<ProxyEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ProxyEntry entity
        modelBuilder.Entity<ProxyEntry>(entity =>
        {
            entity.ToTable("proxy_entries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(e => e.IpAddress)
                .HasColumnName("ip_address")
                .HasMaxLength(45)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            // Index on IP address for faster lookups
            entity.HasIndex(e => e.IpAddress)
                .HasDatabaseName("ix_proxy_entries_ip_address");
        });
    }
}

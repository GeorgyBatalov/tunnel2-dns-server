using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tunnel2.DnsServer.Data;

/// <summary>
/// Proxy entry entity representing an entry point in the database.
/// </summary>
[Table("proxy_entries")]
public sealed class ProxyEntry
{
    /// <summary>
    /// Unique identifier of the proxy entry.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// IP address assigned to this proxy entry (e.g., "203.0.113.10").
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    [Column("ip_address")]
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the entry was created.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the entry was last updated.
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

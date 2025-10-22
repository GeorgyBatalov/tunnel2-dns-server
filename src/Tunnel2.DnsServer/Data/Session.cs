using System.ComponentModel.DataAnnotations;

namespace Tunnel2.DnsServer.Data;

/// <summary>
/// Session entity representing an active tunnel session in the database.
/// </summary>
public sealed class Session
{
    /// <summary>
    /// Unique identifier of the session (matches tunnel SessionId).
    /// </summary>
    [Key]
    public Guid SessionId { get; init; }

    /// <summary>
    /// Hostname (subdomain) for this session.
    /// Can be auto-generated (e.g., "a1b2c3d4-e1") or custom (e.g., "my-app").
    /// Full FQDN will be: {Hostname}.tunnel4.com
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Hostname { get; init; } = string.Empty;

    /// <summary>
    /// IP address of the proxy entry handling this session (e.g., "203.0.113.10").
    /// </summary>
    [Required]
    [MaxLength(45)] // IPv6 max length
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the session expires.
    /// Used by background worker to clean up expired sessions.
    /// </summary>
    public DateTime ExpiresAt { get; init; }
}

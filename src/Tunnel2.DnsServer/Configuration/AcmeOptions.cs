namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for ACME DNS-01 challenge TXT records.
/// Can be loaded from appsettings.json and/or HashiCorp Vault (Vault has priority).
/// </summary>
public sealed class AcmeOptions
{
    /// <summary>
    /// Gets the first ACME challenge token for _acme-challenge TXT record.
    /// </summary>
    public string? AcmeChallenge1 { get; init; }

    /// <summary>
    /// Gets the second ACME challenge token for _acme-challenge TXT record.
    /// Let's Encrypt requires two separate TXT records for wildcard certificates.
    /// </summary>
    public string? AcmeChallenge2 { get; init; }

    /// <summary>
    /// Gets the Time-To-Live for ACME challenge TXT records.
    /// Default: 1 minute (00:01:00).
    /// </summary>
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(1);
}

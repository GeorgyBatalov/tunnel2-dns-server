namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Provides ACME challenge tokens and TTL for DNS TXT records.
/// </summary>
public interface IAcmeTokensProvider
{
    /// <summary>
    /// Gets the list of ACME challenge tokens for TXT records.
    /// Typically returns 1-2 tokens (Let's Encrypt wildcard requires 2).
    /// </summary>
    /// <returns>Collection of non-empty ACME challenge token strings.</returns>
    IEnumerable<string> GetTokens();

    /// <summary>
    /// Gets the Time-To-Live for ACME challenge TXT records.
    /// </summary>
    /// <returns>TTL as TimeSpan.</returns>
    TimeSpan GetTtl();
}

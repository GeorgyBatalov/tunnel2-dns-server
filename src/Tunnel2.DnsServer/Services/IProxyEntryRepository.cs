namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Repository for proxy entry data access.
/// </summary>
public interface IProxyEntryRepository
{
    /// <summary>
    /// Gets the IP address for a given proxy entry ID.
    /// </summary>
    /// <param name="proxyEntryId">The proxy entry ID (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The IP address if found, null otherwise.</returns>
    Task<string?> GetIpAddressAsync(Guid proxyEntryId, CancellationToken cancellationToken = default);
}

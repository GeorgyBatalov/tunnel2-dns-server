namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Interface for caching session IP address mappings (new mode only).
/// </summary>
public interface ISessionIpAddressCache
{
    /// <summary>
    /// Tries to get the cached IP address for a session.
    /// If found, extends the sliding expiration.
    /// </summary>
    /// <param name="sessionKey">The session key (e.g., "my-app-e1").</param>
    /// <param name="ipAddress">The cached IP address if found.</param>
    /// <returns>True if the IP address was found in cache; otherwise, false.</returns>
    bool TryGetIpAddress(string sessionKey, out string? ipAddress);

    /// <summary>
    /// Adds or updates the IP address for a session in cache.
    /// </summary>
    /// <param name="sessionKey">The session key (e.g., "my-app-e1").</param>
    /// <param name="ipAddress">The IP address to cache.</param>
    void SetIpAddress(string sessionKey, string ipAddress);

    /// <summary>
    /// Removes a session from cache.
    /// </summary>
    /// <param name="sessionKey">The session key to remove.</param>
    void Remove(string sessionKey);

    /// <summary>
    /// Clears all cached sessions.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the current number of cached sessions.
    /// </summary>
    /// <returns>Number of cached sessions.</returns>
    int GetCachedSessionCount();
}

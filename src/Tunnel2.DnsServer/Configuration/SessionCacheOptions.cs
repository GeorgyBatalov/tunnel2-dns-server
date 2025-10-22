namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for in-memory session cache (new mode only).
/// Legacy mode does not use cache as it always returns the same static IP.
/// </summary>
public sealed class SessionCacheOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether session caching is enabled.
    /// Default: true.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Gets or sets the sliding expiration time for cached sessions.
    /// If a session is not accessed within this time, it will be evicted from cache.
    /// Each DNS query for the session extends the expiration by this duration.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan SlidingExpiration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the absolute expiration time for cached sessions.
    /// Sessions will be evicted after this time regardless of access.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan AbsoluteExpiration { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of cached sessions.
    /// When limit is reached, least recently used sessions will be evicted.
    /// Default: 10000.
    /// </summary>
    public int MaxCachedSessions { get; init; } = 10000;
}

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// In-memory cache for session IP address mappings with sliding expiration.
/// Used for new mode to reduce database load.
/// </summary>
public sealed class SessionIpAddressCache : ISessionIpAddressCache, IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly IOptionsMonitor<SessionCacheOptions> _cacheOptionsMonitor;
    private readonly ILogger<SessionIpAddressCache> _logger;

    public SessionIpAddressCache(
        IMemoryCache memoryCache,
        IOptionsMonitor<SessionCacheOptions> cacheOptionsMonitor,
        ILogger<SessionIpAddressCache> logger)
    {
        _memoryCache = memoryCache;
        _cacheOptionsMonitor = cacheOptionsMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool TryGetIpAddress(string sessionKey, out string? ipAddress)
    {
        SessionCacheOptions options = _cacheOptionsMonitor.CurrentValue;

        if (!options.IsEnabled)
        {
            ipAddress = null;
            return false;
        }

        if (_memoryCache.TryGetValue(sessionKey, out string? cachedIp))
        {
            _logger.LogDebug("Cache HIT for session {SessionKey}: {IpAddress}", sessionKey, cachedIp);
            ipAddress = cachedIp;
            return true;
        }

        _logger.LogDebug("Cache MISS for session {SessionKey}", sessionKey);
        ipAddress = null;
        return false;
    }

    /// <inheritdoc />
    public void SetIpAddress(string sessionKey, string ipAddress)
    {
        SessionCacheOptions options = _cacheOptionsMonitor.CurrentValue;

        if (!options.IsEnabled)
        {
            return;
        }

        MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(options.SlidingExpiration)
            .SetAbsoluteExpiration(options.AbsoluteExpiration)
            .SetSize(1) // Each entry counts as 1 toward size limit
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                _logger.LogDebug("Session {SessionKey} evicted from cache. Reason: {Reason}",
                    key, reason);
            });

        _memoryCache.Set(sessionKey, ipAddress, cacheEntryOptions);

        _logger.LogDebug("Cached IP address for session {SessionKey}: {IpAddress} (Sliding: {Sliding}, Absolute: {Absolute})",
            sessionKey, ipAddress, options.SlidingExpiration, options.AbsoluteExpiration);
    }

    /// <inheritdoc />
    public void Remove(string sessionKey)
    {
        _memoryCache.Remove(sessionKey);
        _logger.LogDebug("Removed session {SessionKey} from cache", sessionKey);
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_memoryCache is MemoryCache memCache)
        {
            memCache.Compact(1.0); // Remove 100% of cache
            _logger.LogInformation("Cache cleared");
        }
    }

    /// <inheritdoc />
    public int GetCachedSessionCount()
    {
        if (_memoryCache is MemoryCache memCache)
        {
            return memCache.Count;
        }

        return 0;
    }

    public void Dispose()
    {
        // MemoryCache is managed by DI container, no need to dispose here
    }
}

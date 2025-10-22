using Microsoft.EntityFrameworkCore;
using Tunnel2.DnsServer.Data;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Repository for proxy entry data access using Entity Framework Core.
/// </summary>
public sealed class ProxyEntryRepository : IProxyEntryRepository
{
    private readonly IDbContextFactory<DnsServerDbContext> _dbContextFactory;
    private readonly ILogger<ProxyEntryRepository> _logger;

    public ProxyEntryRepository(
        IDbContextFactory<DnsServerDbContext> dbContextFactory,
        ILogger<ProxyEntryRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<string?> GetIpAddressAsync(Guid proxyEntryId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using DnsServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            ProxyEntry? proxyEntry = await dbContext.ProxyEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == proxyEntryId, cancellationToken);

            if (proxyEntry != null)
            {
                _logger.LogDebug("Found IP address {IpAddress} for proxy entry {ProxyEntryId}",
                    proxyEntry.IpAddress, proxyEntryId);
                return proxyEntry.IpAddress;
            }

            _logger.LogDebug("Proxy entry {ProxyEntryId} not found in database", proxyEntryId);
            return null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error querying database for proxy entry {ProxyEntryId}", proxyEntryId);
            return null;
        }
    }
}

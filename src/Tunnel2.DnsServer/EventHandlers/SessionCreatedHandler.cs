using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Data;
using Tunnel2.DnsServer.Services;
using Tunnel2.TunnelServer.Infrastructure.Contracts.Events;

namespace Tunnel2.DnsServer.EventHandlers;

/// <summary>
/// Handles SessionCreated events from RabbitMQ and creates DNS records for HTTP sessions.
/// </summary>
public sealed class SessionCreatedHandler
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IOptionsMonitor<EntryIpAddressMapOptions> _entryIpAddressMapOptions;
    private readonly ILogger<SessionCreatedHandler> _logger;

    public SessionCreatedHandler(
        ISessionRepository sessionRepository,
        IOptionsMonitor<EntryIpAddressMapOptions> entryIpAddressMapOptions,
        ILogger<SessionCreatedHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _entryIpAddressMapOptions = entryIpAddressMapOptions;
        _logger = logger;
    }

    public async Task HandleAsync(SessionCreated sessionCreated, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Extract hostname from TunnelHost (already contains full hostname)
            string hostname = sessionCreated.TunnelHost;

            // 2. Get IP address from EntryIpAddressMapOptions using ProxyEntryId
            string? ipAddress = GetIpAddressForEntry(sessionCreated.ProxyEntryId);

            if (string.IsNullOrEmpty(ipAddress))
            {
                _logger.LogWarning(
                    "No IP address mapping found for ProxyEntryId {ProxyEntryId}, SessionId {SessionId}. Skipping DNS record creation.",
                    sessionCreated.ProxyEntryId,
                    sessionCreated.SessionId);
                return;
            }

            // 3. Calculate expiration time (CreatedAt + TTL)
            DateTime expiresAt = (sessionCreated.Timestamp + sessionCreated.Ttl).UtcDateTime;

            // 4. Create session entity
            var session = new Session
            {
                SessionId = sessionCreated.SessionId,
                Hostname = hostname,
                IpAddress = ipAddress,
                CreatedAt = sessionCreated.Timestamp.UtcDateTime,
                ExpiresAt = expiresAt
            };

            // 5. Save to database
            await _sessionRepository.UpsertAsync(session, cancellationToken);

            _logger.LogInformation(
                "DNS record created: SessionId={SessionId}, Hostname={Hostname}, IpAddress={IpAddress}, ExpiresAt={ExpiresAt}",
                session.SessionId,
                session.Hostname,
                session.IpAddress,
                session.ExpiresAt);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to handle SessionCreated event for SessionId {SessionId}",
                sessionCreated.SessionId);
            throw;
        }
    }

    private string? GetIpAddressForEntry(string proxyEntryId)
    {
        var map = _entryIpAddressMapOptions.CurrentValue.Map;

        if (map.TryGetValue(proxyEntryId, out string? ipAddress))
        {
            return ipAddress;
        }

        _logger.LogDebug("ProxyEntryId {ProxyEntryId} not found in EntryIpAddressMap", proxyEntryId);
        return null;
    }
}

using Tunnel2.DnsServer.Data;
using Tunnel2.DnsServer.Services;
using Tunnel2.TunnelServer.Infrastructure.Contracts.Events;

namespace Tunnel2.DnsServer.EventHandlers;

/// <summary>
/// Handles SessionClosed events from RabbitMQ and removes DNS records for HTTP sessions.
/// </summary>
public sealed class SessionClosedHandler
{
    private readonly DnsServerDbContext _dbContext;
    private readonly ILogger<SessionClosedHandler> _logger;

    public SessionClosedHandler(
        DnsServerDbContext dbContext,
        ILogger<SessionClosedHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(SessionClosed sessionClosed, CancellationToken cancellationToken)
    {
        try
        {
            // Find and delete session by SessionId
            Session? session = await _dbContext.Sessions
                .FindAsync(new object[] { sessionClosed.SessionId }, cancellationToken);

            if (session != null)
            {
                _dbContext.Sessions.Remove(session);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "DNS record deleted: SessionId={SessionId}, Hostname={Hostname}, Reason={Reason}",
                    sessionClosed.SessionId,
                    session.Hostname,
                    sessionClosed.Reason);
            }
            else
            {
                _logger.LogDebug(
                    "Session {SessionId} not found in database (already deleted or never created). Reason: {Reason}",
                    sessionClosed.SessionId,
                    sessionClosed.Reason);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to handle SessionClosed event for SessionId {SessionId}",
                sessionClosed.SessionId);
            throw;
        }
    }
}

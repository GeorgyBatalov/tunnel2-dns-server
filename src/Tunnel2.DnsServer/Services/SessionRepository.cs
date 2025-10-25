using Microsoft.EntityFrameworkCore;
using Tunnel2.DnsServer.Data;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Repository for session data access using Entity Framework Core.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly DnsServerDbContext _dbContext;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(
        DnsServerDbContext dbContext,
        ILogger<SessionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Session?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        try
        {
            Session? session = await _dbContext.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Hostname == hostname, cancellationToken);

            if (session != null)
            {
                _logger.LogDebug("Found session {SessionId} for hostname {Hostname}",
                    session.SessionId, hostname);
            }
            else
            {
                _logger.LogDebug("Session with hostname {Hostname} not found in database", hostname);
            }

            return session;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error querying database for hostname {Hostname}", hostname);
            return null;
        }
    }

    public async Task UpsertAsync(Session session, CancellationToken cancellationToken = default)
    {
        try
        {
            Session? existingSession = await _dbContext.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, cancellationToken);

            if (existingSession != null)
            {
                _dbContext.Sessions.Remove(existingSession);
            }

            await _dbContext.Sessions.AddAsync(session, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Session {SessionId} with hostname {Hostname} saved to database",
                session.SessionId, session.Hostname);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error saving session {SessionId} to database", session.SessionId);
            throw;
        }
    }

    public async Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DateTime now = DateTime.UtcNow;
            List<Session> expiredSessions = await _dbContext.Sessions
                .Where(s => s.ExpiresAt < now)
                .ToListAsync(cancellationToken);

            if (expiredSessions.Count > 0)
            {
                _dbContext.Sessions.RemoveRange(expiredSessions);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} expired sessions from database", expiredSessions.Count);
            }

            return expiredSessions.Count;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error deleting expired sessions from database");
            return 0;
        }
    }
}

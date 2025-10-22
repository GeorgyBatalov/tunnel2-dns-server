using Microsoft.EntityFrameworkCore;
using Tunnel2.DnsServer.Data;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Repository for session data access using Entity Framework Core.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly IDbContextFactory<DnsServerDbContext> _dbContextFactory;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(
        IDbContextFactory<DnsServerDbContext> dbContextFactory,
        ILogger<SessionRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<Session?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
    {
        try
        {
            await using DnsServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            Session? session = await dbContext.Sessions
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
            await using DnsServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            Session? existingSession = await dbContext.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == session.SessionId, cancellationToken);

            if (existingSession != null)
            {
                dbContext.Sessions.Remove(existingSession);
            }

            await dbContext.Sessions.AddAsync(session, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

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
            await using DnsServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            DateTime now = DateTime.UtcNow;
            int deletedCount = await dbContext.Sessions
                .Where(s => s.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Deleted {Count} expired sessions from database", deletedCount);
            }

            return deletedCount;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error deleting expired sessions from database");
            return 0;
        }
    }
}

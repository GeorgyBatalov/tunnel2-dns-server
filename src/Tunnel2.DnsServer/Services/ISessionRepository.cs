using Tunnel2.DnsServer.Data;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Repository for session data access.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Gets a session by hostname.
    /// </summary>
    /// <param name="hostname">The hostname to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session if found, null otherwise.</returns>
    Task<Session?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a session in the database.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes expired sessions from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of deleted sessions.</returns>
    Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default);
}

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Background service that periodically cleans up expired sessions from the database.
/// </summary>
public sealed class SessionCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval;

    public SessionCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SessionCleanupBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Read cleanup interval from configuration, default to 5 minutes
        _cleanupInterval = configuration.GetValue<TimeSpan>("SessionCleanupOptions:Interval", TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup background service started. Cleanup interval: {Interval}", _cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, service is stopping
                _logger.LogInformation("Session cleanup background service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in session cleanup background service");
                // Continue running even if cleanup fails
            }
        }

        _logger.LogInformation("Session cleanup background service stopped");
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting cleanup of expired sessions");

            // Create a scope to resolve scoped services
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            ISessionRepository sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();

            int deletedCount = await sessionRepository.DeleteExpiredSessionsAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", deletedCount);
            }
            else
            {
                _logger.LogDebug("No expired sessions to clean up");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired sessions");
        }
    }
}

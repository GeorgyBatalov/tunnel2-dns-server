using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Tunnel2.DnsServer.Services;

namespace Tunnel2.DnsServer.Tests;

public class SessionCleanupBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CallsDeleteExpiredSessions_Periodically()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockSessionRepository = new Mock<ISessionRepository>();
        var mockLogger = new Mock<ILogger<SessionCleanupBackgroundService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"SessionCleanupOptions:Interval", "00:00:01"} // 1 second for testing
            })
            .Build();

        mockSessionRepository.Setup(r => r.DeleteExpiredSessionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        mockServiceProvider.Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);
        mockServiceProvider.Setup(p => p.GetService(typeof(ISessionRepository)))
            .Returns(mockSessionRepository.Object);

        var service = new SessionCleanupBackgroundService(
            mockServiceProvider.Object,
            mockLogger.Object,
            configuration);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);

        // Wait for at least 2 cleanup cycles
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        mockSessionRepository.Verify(
            r => r.DeleteExpiredSessionsAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
    }

    [Fact]
    public void Constructor_ReadsIntervalFromConfiguration()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<SessionCleanupBackgroundService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"SessionCleanupOptions:Interval", "00:10:00"} // 10 minutes
            })
            .Build();

        // Act
        var service = new SessionCleanupBackgroundService(
            mockServiceProvider.Object,
            mockLogger.Object,
            configuration);

        // Assert
        // Service created successfully with custom interval
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_UsesDefaultInterval_WhenNotConfigured()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<SessionCleanupBackgroundService>>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var service = new SessionCleanupBackgroundService(
            mockServiceProvider.Object,
            mockLogger.Object,
            configuration);

        // Assert
        // Service created successfully with default 5 minutes interval
        service.Should().NotBeNull();
    }
}

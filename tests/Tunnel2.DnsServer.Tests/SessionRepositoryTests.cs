using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Tunnel2.DnsServer.Data;
using Tunnel2.DnsServer.Services;

namespace Tunnel2.DnsServer.Tests;

public class SessionRepositoryTests : IDisposable
{
    private readonly DnsServerDbContext _dbContext;
    private readonly IDbContextFactory<DnsServerDbContext> _dbContextFactory;
    private readonly SessionRepository _repository;

    public SessionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DnsServerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DnsServerDbContext(options);

        var mockFactory = new Mock<IDbContextFactory<DnsServerDbContext>>();
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_dbContext);
        _dbContextFactory = mockFactory.Object;

        var mockLogger = new Mock<ILogger<SessionRepository>>();
        _repository = new SessionRepository(_dbContextFactory, mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetByHostnameAsync_WhenSessionExists_ReturnsSession()
    {
        // Arrange
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "test-session",
            IpAddress = "203.0.113.10",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByHostnameAsync("test-session");

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(session.SessionId);
        result.Hostname.Should().Be("test-session");
        result.IpAddress.Should().Be("203.0.113.10");
    }

    [Fact]
    public async Task GetByHostnameAsync_WhenSessionDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByHostnameAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_WhenSessionDoesNotExist_CreatesNewSession()
    {
        // Arrange
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "new-session",
            IpAddress = "203.0.113.20",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        await _repository.UpsertAsync(session);

        // Assert
        var savedSession = await _dbContext.Sessions.FindAsync(session.SessionId);
        savedSession.Should().NotBeNull();
        savedSession!.Hostname.Should().Be("new-session");
        savedSession.IpAddress.Should().Be("203.0.113.20");
    }

    [Fact]
    public async Task UpsertAsync_WhenSessionExists_UpdatesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var originalSession = new Session
        {
            SessionId = sessionId,
            Hostname = "update-test",
            IpAddress = "203.0.113.30",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _dbContext.Sessions.AddAsync(originalSession);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var updatedSession = new Session
        {
            SessionId = sessionId,
            Hostname = "update-test-new",
            IpAddress = "203.0.113.31",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        };

        // Act
        await _repository.UpsertAsync(updatedSession);

        // Assert
        var savedSession = await _dbContext.Sessions.FindAsync(sessionId);
        savedSession.Should().NotBeNull();
        savedSession!.Hostname.Should().Be("update-test-new");
        savedSession.IpAddress.Should().Be("203.0.113.31");
    }

    [Fact]
    public async Task DeleteExpiredSessionsAsync_RemovesOnlyExpiredSessions()
    {
        // Arrange
        var expiredSession1 = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "expired-1",
            IpAddress = "203.0.113.40",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        var expiredSession2 = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "expired-2",
            IpAddress = "203.0.113.41",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-30)
        };

        var activeSession = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "active",
            IpAddress = "203.0.113.42",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _dbContext.Sessions.AddRangeAsync(expiredSession1, expiredSession2, activeSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteExpiredSessionsAsync();

        // Assert
        deletedCount.Should().Be(2);

        var remainingSessions = await _dbContext.Sessions.ToListAsync();
        remainingSessions.Should().HaveCount(1);
        remainingSessions[0].SessionId.Should().Be(activeSession.SessionId);
    }

    [Fact]
    public async Task DeleteExpiredSessionsAsync_WhenNoExpiredSessions_ReturnsZero()
    {
        // Arrange
        var activeSession = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "active",
            IpAddress = "203.0.113.50",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await _dbContext.Sessions.AddAsync(activeSession);
        await _dbContext.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteExpiredSessionsAsync();

        // Assert
        deletedCount.Should().Be(0);
        var remainingSessions = await _dbContext.Sessions.ToListAsync();
        remainingSessions.Should().HaveCount(1);
    }
}

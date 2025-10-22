using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tunnel2.DnsServer.Data;

namespace Tunnel2.DnsServer.Tests;

public class SessionIntegrationTests : IDisposable
{
    private readonly DnsServerDbContext _dbContext;

    public SessionIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DnsServerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DnsServerDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Session_CanBeSaved_AndRetrieved()
    {
        // Arrange
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "integration-test",
            IpAddress = "203.0.113.100",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.Sessions.FindAsync(session.SessionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.SessionId.Should().Be(session.SessionId);
        retrieved.Hostname.Should().Be("integration-test");
        retrieved.IpAddress.Should().Be("203.0.113.100");
    }

    [Fact]
    public async Task Session_Index_OnHostname_WorksCorrectly()
    {
        // Arrange
        var sessions = new[]
        {
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "host-a",
                IpAddress = "203.0.113.1",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "host-b",
                IpAddress = "203.0.113.2",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            },
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "host-c",
                IpAddress = "203.0.113.3",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };

        await _dbContext.Sessions.AddRangeAsync(sessions);
        await _dbContext.SaveChangesAsync();

        // Act
        var found = await _dbContext.Sessions
            .Where(s => s.Hostname == "host-b")
            .FirstOrDefaultAsync();

        // Assert
        found.Should().NotBeNull();
        found!.IpAddress.Should().Be("203.0.113.2");
    }

    [Fact]
    public async Task Session_Index_OnExpiresAt_WorksCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var sessions = new[]
        {
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "expires-soon",
                IpAddress = "203.0.113.10",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5)
            },
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "expires-later",
                IpAddress = "203.0.113.11",
                CreatedAt = now,
                ExpiresAt = now.AddHours(2)
            },
            new Session
            {
                SessionId = Guid.NewGuid(),
                Hostname = "already-expired",
                IpAddress = "203.0.113.12",
                CreatedAt = now.AddHours(-2),
                ExpiresAt = now.AddHours(-1)
            }
        };

        await _dbContext.Sessions.AddRangeAsync(sessions);
        await _dbContext.SaveChangesAsync();

        // Act
        var expiredSessions = await _dbContext.Sessions
            .Where(s => s.ExpiresAt < now)
            .ToListAsync();

        // Assert
        expiredSessions.Should().HaveCount(1);
        expiredSessions[0].Hostname.Should().Be("already-expired");
    }

    [Fact]
    public async Task Session_WithLongHostname_CanBeSaved()
    {
        // Arrange
        var longHostname = new string('a', 255); // Max length
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = longHostname,
            IpAddress = "203.0.113.200",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.Sessions.FindAsync(session.SessionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Hostname.Should().HaveLength(255);
    }

    [Fact]
    public async Task Session_WithIpv6Address_CanBeSaved()
    {
        // Arrange
        var session = new Session
        {
            SessionId = Guid.NewGuid(),
            Hostname = "ipv6-test",
            IpAddress = "2001:0db8:85a3:0000:0000:8a2e:0370:7334",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.Sessions.FindAsync(session.SessionId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.IpAddress.Should().Be("2001:0db8:85a3:0000:0000:8a2e:0370:7334");
    }
}

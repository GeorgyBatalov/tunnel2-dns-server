using FluentAssertions;
using Makaretu.Dns;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Services;

namespace Tunnel2.DnsServer.Tests;

/// <summary>
/// Tests for Makaretu DNS request handler.
/// </summary>
public class MakaretuDnsRequestHandlerTests
{
    private readonly MakaretuDnsRequestHandler _handler;

    public MakaretuDnsRequestHandlerTests()
    {
        DnsServerOptions dnsServerOptions = new DnsServerOptions
        {
            AuthoritativeZones = new List<string> { "tunnel4.com" },
            ResponseTtlSeconds = new ResponseTtlOptions
            {
                LegacyA = 300,
                NewA = 30,
                Txt = 60,
                Negative = 5
            }
        };

        LegacyModeOptions legacyModeOptions = new LegacyModeOptions
        {
            IsEnabled = true,
            LegacyStaticIpAddress = "203.0.113.42"
        };

        EntryIpAddressMapOptions entryIpMapOptions = new EntryIpAddressMapOptions
        {
            Map = new Dictionary<string, string>
            {
                { "e1", "203.0.113.10" },
                { "e2", "203.0.113.11" }
            }
        };

        SessionCacheOptions cacheOptions = new SessionCacheOptions
        {
            IsEnabled = true,
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpiration = TimeSpan.FromHours(1),
            MaxCachedSessions = 100
        };

        ILogger<MakaretuDnsRequestHandler> logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<MakaretuDnsRequestHandler>();

        ILogger<SessionIpAddressCache> cacheLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<SessionIpAddressCache>();

        // Create memory cache
        MemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = cacheOptions.MaxCachedSessions });

        // Create session cache
        ISessionIpAddressCache sessionCache = new SessionIpAddressCache(
            memoryCache,
            new OptionsMonitorWrapper<SessionCacheOptions>(cacheOptions),
            cacheLogger);

        // Create mock ACME tokens provider
        IAcmeTokensProvider acmeTokensProvider = new TestAcmeTokensProvider();

        // Create mock session repository
        ISessionRepository sessionRepository = new TestSessionRepository();

        _handler = new MakaretuDnsRequestHandler(
            logger,
            new OptionsMonitorWrapper<DnsServerOptions>(dnsServerOptions),
            new OptionsMonitorWrapper<LegacyModeOptions>(legacyModeOptions),
            new OptionsMonitorWrapper<EntryIpAddressMapOptions>(entryIpMapOptions),
            acmeTokensProvider,
            sessionCache,
            sessionRepository);
    }

    [Fact]
    public void HandleRequest_LegacyGuidDomain_ShouldReturnStaticIp()
    {
        // Arrange
        Message request = new Message { QR = false };
        request.Questions.Add(new Question
        {
            Name = "2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com",
            Type = DnsType.A,
            Class = DnsClass.IN
        });

        byte[] requestData = request.ToByteArray();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();

        Message response = new Message();
        response.Read(responseData, 0, responseData.Length);

        response.Answers.Should().HaveCount(1);
        ARecord answer = response.Answers[0] as ARecord;
        answer.Should().NotBeNull();
        answer!.Address.ToString().Should().Be("203.0.113.42");
        answer.TTL.TotalSeconds.Should().Be(300);
    }

    [Fact]
    public void HandleRequest_NewFormatDomain_ShouldReturnMappedIp()
    {
        // Arrange
        Message request = new Message { QR = false };
        request.Questions.Add(new Question
        {
            Name = "my-app-e1.tunnel4.com",
            Type = DnsType.A,
            Class = DnsClass.IN
        });

        byte[] requestData = request.ToByteArray();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();

        Message response = new Message();
        response.Read(responseData, 0, responseData.Length);

        response.Answers.Should().HaveCount(1);
        ARecord answer = response.Answers[0] as ARecord;
        answer.Should().NotBeNull();
        answer!.Address.ToString().Should().Be("203.0.113.10");
        answer.TTL.TotalSeconds.Should().Be(30);
    }

    [Fact]
    public void HandleRequest_UnknownDomain_ShouldReturnNxDomain()
    {
        // Arrange
        Message request = new Message { QR = false };
        request.Questions.Add(new Question
        {
            Name = "unknown.tunnel4.com",
            Type = DnsType.A,
            Class = DnsClass.IN
        });

        byte[] requestData = request.ToByteArray();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();

        Message response = new Message();
        response.Read(responseData, 0, responseData.Length);

        response.Answers.Should().BeEmpty();
        response.Status.Should().Be(MessageStatus.NameError); // NXDOMAIN
    }

    [Fact]
    public void HandleRequest_NonAuthoritativeZone_ShouldReturnRefused()
    {
        // Arrange
        Message request = new Message { QR = false };
        request.Questions.Add(new Question
        {
            Name = "example.com",
            Type = DnsType.A,
            Class = DnsClass.IN
        });

        byte[] requestData = request.ToByteArray();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();

        Message response = new Message();
        response.Read(responseData, 0, responseData.Length);

        response.Status.Should().Be(MessageStatus.Refused);
    }

    /// <summary>
    /// Simple test implementation of IAcmeTokensProvider.
    /// </summary>
    private class TestAcmeTokensProvider : IAcmeTokensProvider
    {
        public IEnumerable<string> GetTokens()
        {
            return Enumerable.Empty<string>();
        }

        public TimeSpan GetTtl()
        {
            return TimeSpan.FromMinutes(1);
        }
    }

    /// <summary>
    /// Simple test implementation of ISessionRepository.
    /// </summary>
    private class TestSessionRepository : ISessionRepository
    {
        public Task<Data.Session?> GetByHostnameAsync(string hostname, CancellationToken cancellationToken = default)
        {
            // Return null (no sessions in mock database)
            return Task.FromResult<Data.Session?>(null);
        }

        public Task UpsertAsync(Data.Session session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int> DeleteExpiredSessionsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    /// <summary>
    /// Simple wrapper for IOptionsMonitor for testing.
    /// </summary>
    private class OptionsMonitorWrapper<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public OptionsMonitorWrapper(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

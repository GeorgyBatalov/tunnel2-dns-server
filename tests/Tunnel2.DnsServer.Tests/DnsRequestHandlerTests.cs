using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Protocol;
using Tunnel2.DnsServer.Services;

namespace Tunnel2.DnsServer.Tests;

/// <summary>
/// Tests for DNS request handler (legacy mode A records).
/// </summary>
public class DnsRequestHandlerTests
{
    private readonly DnsRequestHandler _handler;
    private readonly DnsServerOptions _dnsServerOptions;
    private readonly LegacyModeOptions _legacyModeOptions;

    public DnsRequestHandlerTests()
    {
        _dnsServerOptions = new DnsServerOptions
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

        _legacyModeOptions = new LegacyModeOptions
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

        ILogger<DnsRequestHandler> logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<DnsRequestHandler>();

        // Create mock ACME tokens provider
        IAcmeTokensProvider acmeTokensProvider = new TestAcmeTokensProvider();

        _handler = new DnsRequestHandler(
            logger,
            Options.Create(_dnsServerOptions),
            Options.Create(_legacyModeOptions),
            Options.Create(entryIpMapOptions),
            acmeTokensProvider);
    }

    /// <summary>
    /// Simple test implementation of IAcmeTokensProvider.
    /// </summary>
    private class TestAcmeTokensProvider : IAcmeTokensProvider
    {
        public IEnumerable<string> GetTokens()
        {
            // Return empty by default for tests
            return Enumerable.Empty<string>();
        }

        public TimeSpan GetTtl()
        {
            return TimeSpan.FromMinutes(1);
        }
    }

    [Fact]
    public void HandleRequest_LegacyGuidDomain_ShouldReturnStaticIp()
    {
        // Arrange
        DnsPacket request = new DnsPacket
        {
            TransactionId = 1234,
            Flags = 0x0100, // Standard query with RD bit
            Questions = new List<DnsQuestion>
            {
                new DnsQuestion
                {
                    Name = "2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com",
                    Type = 1, // A
                    Class = 1  // IN
                }
            }
        };

        byte[] requestData = request.BuildQuery();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();
        DnsPacket response = DnsPacket.Parse(responseData);

        response.TransactionId.Should().Be(1234);
        response.Answers.Should().HaveCount(1);

        DnsResourceRecord answer = response.Answers[0];
        answer.Type.Should().Be(1); // A record
        answer.Ttl.Should().Be(300); // Legacy TTL
        answer.Data.Should().Be("203.0.113.42");
    }

    [Fact]
    public void HandleRequest_NewFormatDomain_ShouldReturnMappedIp()
    {
        // Arrange
        DnsPacket request = new DnsPacket
        {
            TransactionId = 5678,
            Flags = 0x0100,
            Questions = new List<DnsQuestion>
            {
                new DnsQuestion
                {
                    Name = "my-app-e1.tunnel4.com",
                    Type = 1, // A
                    Class = 1  // IN
                }
            }
        };

        byte[] requestData = request.BuildQuery();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();
        DnsPacket response = DnsPacket.Parse(responseData);

        response.TransactionId.Should().Be(5678);
        response.Answers.Should().HaveCount(1);

        DnsResourceRecord answer = response.Answers[0];
        answer.Type.Should().Be(1); // A record
        answer.Ttl.Should().Be(30); // New mode TTL
        answer.Data.Should().Be("203.0.113.10");
    }

    [Fact]
    public void HandleRequest_UnknownDomain_ShouldReturnNxDomain()
    {
        // Arrange
        DnsPacket request = new DnsPacket
        {
            TransactionId = 9999,
            Flags = 0x0100,
            Questions = new List<DnsQuestion>
            {
                new DnsQuestion
                {
                    Name = "unknown.tunnel4.com",
                    Type = 1, // A
                    Class = 1  // IN
                }
            }
        };

        byte[] requestData = request.BuildQuery();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();
        DnsPacket response = DnsPacket.Parse(responseData);

        response.TransactionId.Should().Be(9999);
        response.Answers.Should().BeEmpty();

        // RCODE should be NXDOMAIN (3)
        int rcode = response.Flags & 0x000F;
        rcode.Should().Be(3);
    }

    [Fact]
    public void HandleRequest_NonAuthoritativeZone_ShouldReturnRefused()
    {
        // Arrange
        DnsPacket request = new DnsPacket
        {
            TransactionId = 1111,
            Flags = 0x0100,
            Questions = new List<DnsQuestion>
            {
                new DnsQuestion
                {
                    Name = "example.com",
                    Type = 1, // A
                    Class = 1  // IN
                }
            }
        };

        byte[] requestData = request.BuildQuery();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();
        DnsPacket response = DnsPacket.Parse(responseData);

        response.TransactionId.Should().Be(1111);
        response.Answers.Should().BeEmpty();

        // RCODE should be REFUSED (5)
        int rcode = response.Flags & 0x000F;
        rcode.Should().Be(5);
    }

    [Fact]
    public void HandleRequest_TxtRecordWithoutData_ShouldReturnNxDomain()
    {
        // Arrange
        DnsPacket request = new DnsPacket
        {
            TransactionId = 2222,
            Flags = 0x0100,
            Questions = new List<DnsQuestion>
            {
                new DnsQuestion
                {
                    Name = "_acme-challenge.test.tunnel4.com",
                    Type = 16, // TXT
                    Class = 1   // IN
                }
            }
        };

        byte[] requestData = request.BuildQuery();

        // Act
        byte[] responseData = _handler.HandleRequest(requestData);

        // Assert
        responseData.Should().NotBeEmpty();
        DnsPacket response = DnsPacket.Parse(responseData);

        response.TransactionId.Should().Be(2222);
        response.Answers.Should().BeEmpty();

        // RCODE should be NXDOMAIN (3)
        int rcode = response.Flags & 0x000F;
        rcode.Should().Be(3);
    }
}

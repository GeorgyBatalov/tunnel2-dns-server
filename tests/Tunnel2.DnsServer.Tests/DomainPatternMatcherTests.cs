using FluentAssertions;
using Tunnel2.DomainNames;

namespace Tunnel2.DnsServer.Tests;

/// <summary>
/// Tests for domain pattern matchers (legacy and new formats).
/// </summary>
public class DomainPatternMatcherTests
{
    [Theory]
    [InlineData("2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com", true, "2a3be342-60f3-48a9-a2c5-e7359e34959a", "tunnel4.com")]
    [InlineData("00000000-0000-0000-0000-000000000000.tunnel4.com", true, "00000000-0000-0000-0000-000000000000", "tunnel4.com")]
    [InlineData("ABC12345-ABCD-ABCD-ABCD-ABCDEFABCDEF.example.org", true, "ABC12345-ABCD-ABCD-ABCD-ABCDEFABCDEF", "example.org")]
    [InlineData("invalid-guid.tunnel4.com", false, null, null)]
    [InlineData("2a3be342-60f3-48a9-a2c5.tunnel4.com", false, null, null)]
    [InlineData("tunnel4.com", false, null, null)]
    [InlineData("", false, null, null)]
    public void LegacyDomainPatternMatcher_ShouldMatchCorrectly(
        string hostname,
        bool shouldMatch,
        string? expectedGuid,
        string? expectedDomain)
    {
        // Arrange
        LegacyDomainPatternMatcher matcher = new LegacyDomainPatternMatcher();

        // Act
        bool result = matcher.TryMatch(hostname, out DomainMatchResult matchResult);

        // Assert
        result.Should().Be(shouldMatch);

        if (shouldMatch)
        {
            matchResult.MatchType.Should().Be(DomainMatchType.Legacy);
            matchResult.LegacyGuid.Should().BeEquivalentTo(expectedGuid);
            matchResult.PublicDomain.Should().BeEquivalentTo(expectedDomain);
        }
        else
        {
            matchResult.MatchType.Should().Be(DomainMatchType.None);
        }
    }

    [Theory]
    [InlineData("my-subdomain-e1.tunnel4.com", true, "my-subdomain", "e1", "tunnel4.com")]
    [InlineData("test-app-e2.tunnel4.com", true, "test-app", "e2", "tunnel4.com")]
    [InlineData("abc-xyz123.example.org", true, "abc", "xyz123", "example.org")]
    [InlineData("very-long-address-name-here-e1.tunnel4.com", true, "very-long-address-name-here", "e1", "tunnel4.com")]
    [InlineData("ab-e1.tunnel4.com", false, null, null, null)] // address too short (min 3 chars)
    [InlineData("e1.tunnel4.com", false, null, null, null)] // no dash, too short
    [InlineData("tunnel4.com", false, null, null, null)]
    [InlineData("", false, null, null, null)]
    public void NewDomainPatternMatcher_ShouldMatchCorrectly(
        string hostname,
        bool shouldMatch,
        string? expectedAddress,
        string? expectedProxyEntryId,
        string? expectedDomain)
    {
        // Arrange
        NewDomainPatternMatcher matcher = new NewDomainPatternMatcher();

        // Act
        bool result = matcher.TryMatch(hostname, out DomainMatchResult matchResult);

        // Assert
        result.Should().Be(shouldMatch);

        if (shouldMatch)
        {
            matchResult.MatchType.Should().Be(DomainMatchType.New);
            matchResult.NewAddress.Should().BeEquivalentTo(expectedAddress);
            matchResult.ProxyEntryId.Should().BeEquivalentTo(expectedProxyEntryId);
            matchResult.PublicDomain.Should().BeEquivalentTo(expectedDomain);
        }
        else
        {
            matchResult.MatchType.Should().Be(DomainMatchType.None);
        }
    }

    [Fact]
    public void LegacyMatcher_ShouldBeCaseInsensitive()
    {
        // Arrange
        LegacyDomainPatternMatcher matcher = new LegacyDomainPatternMatcher();
        string hostnameUpper = "2A3BE342-60F3-48A9-A2C5-E7359E34959A.TUNNEL4.COM";
        string hostnameLower = "2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com";

        // Act
        bool resultUpper = matcher.TryMatch(hostnameUpper, out DomainMatchResult matchUpper);
        bool resultLower = matcher.TryMatch(hostnameLower, out DomainMatchResult matchLower);

        // Assert
        resultUpper.Should().BeTrue();
        resultLower.Should().BeTrue();
        matchUpper.MatchType.Should().Be(DomainMatchType.Legacy);
        matchLower.MatchType.Should().Be(DomainMatchType.Legacy);
    }

    [Fact]
    public void NewMatcher_ShouldBeCaseInsensitive()
    {
        // Arrange
        NewDomainPatternMatcher matcher = new NewDomainPatternMatcher();
        string hostnameUpper = "MY-SUBDOMAIN-E1.TUNNEL4.COM";
        string hostnameLower = "my-subdomain-e1.tunnel4.com";

        // Act
        bool resultUpper = matcher.TryMatch(hostnameUpper, out DomainMatchResult matchUpper);
        bool resultLower = matcher.TryMatch(hostnameLower, out DomainMatchResult matchLower);

        // Assert
        resultUpper.Should().BeTrue();
        resultLower.Should().BeTrue();
        matchUpper.MatchType.Should().Be(DomainMatchType.New);
        matchLower.MatchType.Should().Be(DomainMatchType.New);
    }
}

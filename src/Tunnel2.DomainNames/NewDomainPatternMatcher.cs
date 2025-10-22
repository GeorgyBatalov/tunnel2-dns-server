using System.Text.RegularExpressions;

namespace Tunnel2.DomainNames;

/// <summary>
/// Matcher for new domain format: {address}-{proxyEntryId}.{domain}
/// Pattern: ^(?&lt;address&gt;[a-z0-9-]{3,128})-(?&lt;proxyEntryId&gt;[a-z0-9][a-z0-9-]{0,31})\.(?&lt;domain&gt;[-a-z0-9.]+)$
/// </summary>
public partial class NewDomainPatternMatcher : IDomainPatternMatcher
{
    [GeneratedRegex(@"^(?<address>[a-z0-9-]{3,128})-(?<proxyEntryId>[a-z0-9][a-z0-9-]{0,31})\.(?<domain>[-a-z0-9.]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex NewPatternRegex();

    /// <inheritdoc />
    public bool TryMatch(string hostname, out DomainMatchResult result)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            result = new DomainMatchResult { MatchType = DomainMatchType.None };
            return false;
        }

        Match match = NewPatternRegex().Match(hostname);
        if (!match.Success)
        {
            result = new DomainMatchResult { MatchType = DomainMatchType.None };
            return false;
        }

        result = new DomainMatchResult
        {
            MatchType = DomainMatchType.New,
            NewAddress = match.Groups["address"].Value,
            ProxyEntryId = match.Groups["proxyEntryId"].Value,
            PublicDomain = match.Groups["domain"].Value
        };

        return true;
    }
}

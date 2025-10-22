using System.Text.RegularExpressions;

namespace Tunnel2.DomainNames;

/// <summary>
/// Matcher for legacy domain format: {guid}.{domain}
/// Pattern: ^(?&lt;guid&gt;[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.(?&lt;domain&gt;[-a-z0-9.]+)$
/// </summary>
public partial class LegacyDomainPatternMatcher : IDomainPatternMatcher
{
    [GeneratedRegex(@"^(?<guid>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.(?<domain>[-a-z0-9.]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyPatternRegex();

    /// <inheritdoc />
    public bool TryMatch(string hostname, out DomainMatchResult result)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            result = new DomainMatchResult { MatchType = DomainMatchType.None };
            return false;
        }

        Match match = LegacyPatternRegex().Match(hostname);
        if (!match.Success)
        {
            result = new DomainMatchResult { MatchType = DomainMatchType.None };
            return false;
        }

        result = new DomainMatchResult
        {
            MatchType = DomainMatchType.Legacy,
            LegacyGuid = match.Groups["guid"].Value,
            PublicDomain = match.Groups["domain"].Value
        };

        return true;
    }
}

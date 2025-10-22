namespace Tunnel2.DomainNames;

/// <summary>
/// Interface for matching domain name patterns (legacy or new format).
/// </summary>
public interface IDomainPatternMatcher
{
    /// <summary>
    /// Attempts to match the given hostname against the pattern.
    /// </summary>
    /// <param name="hostname">The hostname to match.</param>
    /// <param name="result">The match result if successful.</param>
    /// <returns>True if the hostname matches the pattern; otherwise, false.</returns>
    bool TryMatch(string hostname, out DomainMatchResult result);
}

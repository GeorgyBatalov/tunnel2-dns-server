namespace Tunnel2.DomainNames;

/// <summary>
/// Represents the result of a domain pattern match.
/// </summary>
public class DomainMatchResult
{
    /// <summary>
    /// Gets the type of match (legacy or new).
    /// </summary>
    public DomainMatchType MatchType { get; init; }

    /// <summary>
    /// Gets the public domain suffix (e.g., "tunnel4.com").
    /// </summary>
    public string PublicDomain { get; init; } = string.Empty;

    /// <summary>
    /// Gets the GUID for legacy format (e.g., "2a3be342-60f3-48a9-a2c5-e7359e34959a").
    /// </summary>
    public string? LegacyGuid { get; init; }

    /// <summary>
    /// Gets the address segment for new format (e.g., "subdomain-name").
    /// </summary>
    public string? NewAddress { get; init; }

    /// <summary>
    /// Gets the proxy entry identifier for new format (e.g., "e1", "e2").
    /// </summary>
    public string? ProxyEntryId { get; init; }
}

/// <summary>
/// Enumeration of domain match types.
/// </summary>
public enum DomainMatchType
{
    /// <summary>
    /// No match found.
    /// </summary>
    None,

    /// <summary>
    /// Legacy format: {guid}.{domain}
    /// </summary>
    Legacy,

    /// <summary>
    /// New format: {address}-{proxyEntryId}.{domain}
    /// </summary>
    New
}

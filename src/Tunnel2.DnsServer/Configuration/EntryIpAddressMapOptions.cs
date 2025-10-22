namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for mapping proxy entry IDs to IP addresses (new mode).
/// </summary>
public class EntryIpAddressMapOptions
{
    /// <summary>
    /// Gets or sets the mapping of proxy entry identifiers to IP addresses.
    /// Example: { "e1": "203.0.113.10", "e2": "203.0.113.11" }
    /// </summary>
    public Dictionary<string, string> Map { get; set; } = new();
}

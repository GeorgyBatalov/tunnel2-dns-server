namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for the DNS server.
/// </summary>
public class DnsServerOptions
{
    /// <summary>
    /// Gets or sets the IPv4 address to listen on.
    /// </summary>
    public string ListenIpv4 { get; set; } = "0.0.0.0";

    /// <summary>
    /// Gets or sets the UDP port to listen on.
    /// </summary>
    public int UdpPort { get; set; } = 53;

    /// <summary>
    /// Gets or sets the TCP port to listen on.
    /// </summary>
    public int TcpPort { get; set; } = 53;

    /// <summary>
    /// Gets or sets the list of authoritative zones this server handles.
    /// </summary>
    public List<string> AuthoritativeZones { get; set; } = new() { "tunnel4.com" };

    /// <summary>
    /// Gets or sets the TTL values for different response types.
    /// </summary>
    public ResponseTtlOptions ResponseTtlSeconds { get; set; } = new();
}

/// <summary>
/// TTL configuration for different DNS response types.
/// </summary>
public class ResponseTtlOptions
{
    /// <summary>
    /// Gets or sets the TTL for legacy A records.
    /// </summary>
    public int LegacyA { get; set; } = 300;

    /// <summary>
    /// Gets or sets the TTL for new A records.
    /// </summary>
    public int NewA { get; set; } = 30;

    /// <summary>
    /// Gets or sets the TTL for TXT records.
    /// </summary>
    public int Txt { get; set; } = 60;

    /// <summary>
    /// Gets or sets the TTL for negative responses (NXDOMAIN).
    /// </summary>
    public int Negative { get; set; } = 5;
}

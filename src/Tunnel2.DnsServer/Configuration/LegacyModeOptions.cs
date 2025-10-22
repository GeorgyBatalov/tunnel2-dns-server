namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for legacy mode operation.
/// </summary>
public class LegacyModeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether legacy mode is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the static IP address to return for all legacy domain queries.
    /// </summary>
    public string LegacyStaticIpAddress { get; set; } = "203.0.113.42";
}

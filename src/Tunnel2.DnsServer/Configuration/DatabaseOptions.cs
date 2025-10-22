namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Database connection configuration options.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;
}

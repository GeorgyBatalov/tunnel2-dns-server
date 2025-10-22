namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Vault configuration for database connection string.
/// </summary>
public sealed class DatabaseVaultOptions
{
    /// <summary>
    /// Whether to use Vault for database connection string.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Vault address (e.g., "http://127.0.0.1:8200").
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// Vault authentication token.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Vault mount point for KV v2 secrets engine (default: "secret").
    /// </summary>
    public string MountPoint { get; init; } = "secret";

    /// <summary>
    /// Path to the secret containing connection string (e.g., "tunnel/tunnel2-dns/database").
    /// </summary>
    public string Path { get; init; } = "tunnel/tunnel2-dns/database";

    /// <summary>
    /// Key name in the secret for connection string (default: "ConnectionString").
    /// </summary>
    public string ConnectionStringKey { get; init; } = "ConnectionString";
}

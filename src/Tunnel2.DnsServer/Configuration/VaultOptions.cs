namespace Tunnel2.DnsServer.Configuration;

/// <summary>
/// Configuration options for HashiCorp Vault integration.
/// Vault values override appsettings.json when enabled.
/// </summary>
public sealed class DnsVaultOptions
{
    /// <summary>
    /// Gets a value indicating whether Vault integration is enabled.
    /// When false, only appsettings.json is used.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the Vault server address (e.g., "http://127.0.0.1:8200").
    /// </summary>
    public string? Address { get; init; }

    /// <summary>
    /// Gets the Vault authentication token.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Gets the Vault KV mount point.
    /// Default: "secret".
    /// </summary>
    public string MountPoint { get; init; } = "secret";

    /// <summary>
    /// Gets the path to the secret in Vault (without mount point prefix).
    /// Default: "tunnel/tunnel2-dns/acme/wildcard".
    /// Example: vault kv get secret/tunnel/tunnel2-dns/acme/wildcard
    /// </summary>
    public string Path { get; init; } = "tunnel/tunnel2-dns/acme/wildcard";
}

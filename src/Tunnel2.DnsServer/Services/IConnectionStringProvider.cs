namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Provides database connection string from Vault or appsettings.json.
/// </summary>
public interface IConnectionStringProvider
{
    /// <summary>
    /// Gets the connection string (Vault has priority over appsettings).
    /// </summary>
    string GetConnectionString();
}

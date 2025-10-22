using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Connection string provider that reads from Vault with fallback to appsettings.json.
/// </summary>
public sealed class VaultBackedConnectionStringProvider : IConnectionStringProvider
{
    private readonly ILogger<VaultBackedConnectionStringProvider> _logger;
    private readonly IOptionsMonitor<DatabaseOptions> _databaseOptionsMonitor;
    private readonly IOptionsMonitor<DatabaseVaultOptions> _vaultOptionsMonitor;
    private readonly IVaultClient? _vaultClient;

    public VaultBackedConnectionStringProvider(
        ILogger<VaultBackedConnectionStringProvider> logger,
        IOptionsMonitor<DatabaseOptions> databaseOptionsMonitor,
        IOptionsMonitor<DatabaseVaultOptions> vaultOptionsMonitor)
    {
        _logger = logger;
        _databaseOptionsMonitor = databaseOptionsMonitor;
        _vaultOptionsMonitor = vaultOptionsMonitor;

        DatabaseVaultOptions vaultOptions = vaultOptionsMonitor.CurrentValue;

        if (vaultOptions.Enabled && !string.IsNullOrWhiteSpace(vaultOptions.Address) &&
            !string.IsNullOrWhiteSpace(vaultOptions.Token))
        {
            try
            {
                IAuthMethodInfo authMethod = new TokenAuthMethodInfo(vaultOptions.Token);
                VaultClientSettings vaultClientSettings = new VaultClientSettings(vaultOptions.Address, authMethod);
                _vaultClient = new VaultClient(vaultClientSettings);

                _logger.LogInformation("Vault client initialized for database connection string at {Address}",
                    vaultOptions.Address);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to initialize Vault client for database connection string");
                _vaultClient = null;
            }
        }
        else
        {
            _logger.LogInformation("Vault disabled or not configured for database connection string, using appsettings.json");
        }
    }

    public string GetConnectionString()
    {
        DatabaseVaultOptions vaultOptions = _vaultOptionsMonitor.CurrentValue;

        // Try Vault first if enabled and client is initialized
        if (_vaultClient != null && vaultOptions.Enabled)
        {
            try
            {
                Secret<SecretData> secret = _vaultClient.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path: vaultOptions.Path, mountPoint: vaultOptions.MountPoint)
                    .GetAwaiter()
                    .GetResult();

                if (secret?.Data?.Data != null &&
                    secret.Data.Data.TryGetValue(vaultOptions.ConnectionStringKey, out object? connectionStringObj) &&
                    connectionStringObj != null)
                {
                    string connectionString = connectionStringObj.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(connectionString))
                    {
                        _logger.LogDebug("Using database connection string from Vault: {Path}/{Key}",
                            vaultOptions.Path, vaultOptions.ConnectionStringKey);

                        return connectionString;
                    }
                }

                _logger.LogWarning("Database connection string not found in Vault at {Path}/{Key}, falling back to appsettings.json",
                    vaultOptions.Path, vaultOptions.ConnectionStringKey);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Error reading database connection string from Vault at {Path}/{Key}, falling back to appsettings.json",
                    vaultOptions.Path, vaultOptions.ConnectionStringKey);
            }
        }

        // Fallback to appsettings.json
        DatabaseOptions databaseOptions = _databaseOptionsMonitor.CurrentValue;
        _logger.LogDebug("Using database connection string from appsettings.json");
        return databaseOptions.ConnectionString;
    }
}

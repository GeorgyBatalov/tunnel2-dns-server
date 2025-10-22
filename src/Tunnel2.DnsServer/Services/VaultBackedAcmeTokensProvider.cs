using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Implementation of IAcmeTokensProvider that reads from HashiCorp Vault (with fallback to appsettings.json).
/// Vault values have priority over local configuration.
/// </summary>
public sealed class VaultBackedAcmeTokensProvider : IAcmeTokensProvider
{
    private readonly IOptionsMonitor<AcmeOptions> _acmeOptionsMonitor;
    private readonly IOptionsMonitor<DnsVaultOptions> _vaultOptionsMonitor;
    private readonly ILogger<VaultBackedAcmeTokensProvider> _logger;
    private IVaultClient? _vaultClient;

    public VaultBackedAcmeTokensProvider(
        IOptionsMonitor<AcmeOptions> acmeOptionsMonitor,
        IOptionsMonitor<DnsVaultOptions> vaultOptionsMonitor,
        ILogger<VaultBackedAcmeTokensProvider> logger)
    {
        _acmeOptionsMonitor = acmeOptionsMonitor;
        _vaultOptionsMonitor = vaultOptionsMonitor;
        _logger = logger;

        InitializeVaultClient();
    }

    private void InitializeVaultClient()
    {
        DnsVaultOptions vaultOptions = _vaultOptionsMonitor.CurrentValue;

        if (!vaultOptions.Enabled)
        {
            _logger.LogInformation("Vault integration is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(vaultOptions.Address) || string.IsNullOrWhiteSpace(vaultOptions.Token))
        {
            _logger.LogWarning("Vault is enabled but Address or Token is missing");
            return;
        }

        try
        {
            IAuthMethodInfo authMethod = new TokenAuthMethodInfo(vaultOptions.Token);
            VaultClientSettings vaultClientSettings = new VaultClientSettings(vaultOptions.Address, authMethod);
            _vaultClient = new VaultClient(vaultClientSettings);

            _logger.LogInformation("Vault client initialized successfully. Address: {VaultAddress}",
                vaultOptions.Address);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to initialize Vault client");
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetTokens()
    {
        // Try Vault first if enabled
        if (_vaultClient != null)
        {
            try
            {
                DnsVaultOptions vaultOptions = _vaultOptionsMonitor.CurrentValue;
                string vaultPath = $"{vaultOptions.MountPoint}/data/{vaultOptions.Path}";

                _logger.LogDebug("Reading ACME tokens from Vault: {VaultPath}", vaultPath);

                Secret<SecretData> secret = _vaultClient.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path: vaultOptions.Path, mountPoint: vaultOptions.MountPoint)
                    .GetAwaiter()
                    .GetResult();

                if (secret?.Data?.Data != null)
                {
                    List<string> tokens = new List<string>();

                    if (secret.Data.Data.TryGetValue("AcmeChallenge1", out object? value1) &&
                        value1 != null &&
                        !string.IsNullOrWhiteSpace(value1.ToString()))
                    {
                        tokens.Add(value1.ToString()!);
                        _logger.LogDebug("Loaded ACME challenge token 1 from Vault");
                    }

                    if (secret.Data.Data.TryGetValue("AcmeChallenge2", out object? value2) &&
                        value2 != null &&
                        !string.IsNullOrWhiteSpace(value2.ToString()))
                    {
                        tokens.Add(value2.ToString()!);
                        _logger.LogDebug("Loaded ACME challenge token 2 from Vault");
                    }

                    if (tokens.Count > 0)
                    {
                        _logger.LogInformation("Returning {Count} ACME token(s) from Vault", tokens.Count);
                        return tokens;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to read ACME tokens from Vault, falling back to appsettings.json");
            }
        }

        // Fallback to appsettings.json
        _logger.LogDebug("Using ACME tokens from appsettings.json");
        return GetTokensFromAppSettings();
    }

    /// <inheritdoc />
    public TimeSpan GetTtl()
    {
        // Try Vault first if enabled
        if (_vaultClient != null)
        {
            try
            {
                DnsVaultOptions vaultOptions = _vaultOptionsMonitor.CurrentValue;

                Secret<SecretData> secret = _vaultClient.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path: vaultOptions.Path, mountPoint: vaultOptions.MountPoint)
                    .GetAwaiter()
                    .GetResult();

                if (secret?.Data?.Data != null &&
                    secret.Data.Data.TryGetValue("Ttl", out object? ttlValue) &&
                    ttlValue != null)
                {
                    string ttlString = ttlValue.ToString()!;
                    if (TimeSpan.TryParse(ttlString, out TimeSpan ttl))
                    {
                        _logger.LogDebug("Using TTL from Vault: {Ttl}", ttl);
                        return ttl;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to read TTL from Vault, using appsettings.json");
            }
        }

        // Fallback to appsettings.json
        TimeSpan appSettingsTtl = _acmeOptionsMonitor.CurrentValue.Ttl;
        _logger.LogDebug("Using TTL from appsettings.json: {Ttl}", appSettingsTtl);
        return appSettingsTtl;
    }

    private IEnumerable<string> GetTokensFromAppSettings()
    {
        AcmeOptions options = _acmeOptionsMonitor.CurrentValue;

        if (!string.IsNullOrWhiteSpace(options.AcmeChallenge1))
        {
            yield return options.AcmeChallenge1!;
        }

        if (!string.IsNullOrWhiteSpace(options.AcmeChallenge2))
        {
            yield return options.AcmeChallenge2!;
        }
    }
}

using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Implementation of IAcmeTokensProvider backed by IOptionsMonitor&lt;AcmeOptions&gt;.
/// Supports hot reload from both appsettings.json and HashiCorp Vault.
/// </summary>
public sealed class OptionsBackedAcmeTokensProvider : IAcmeTokensProvider
{
    private readonly IOptionsMonitor<AcmeOptions> _acmeOptionsMonitor;
    private readonly ILogger<OptionsBackedAcmeTokensProvider> _logger;

    public OptionsBackedAcmeTokensProvider(
        IOptionsMonitor<AcmeOptions> acmeOptionsMonitor,
        ILogger<OptionsBackedAcmeTokensProvider> logger)
    {
        _acmeOptionsMonitor = acmeOptionsMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetTokens()
    {
        AcmeOptions options = _acmeOptionsMonitor.CurrentValue;

        if (!string.IsNullOrWhiteSpace(options.AcmeChallenge1))
        {
            _logger.LogDebug("Returning ACME challenge token 1");
            yield return options.AcmeChallenge1!;
        }

        if (!string.IsNullOrWhiteSpace(options.AcmeChallenge2))
        {
            _logger.LogDebug("Returning ACME challenge token 2");
            yield return options.AcmeChallenge2!;
        }
    }

    /// <inheritdoc />
    public TimeSpan GetTtl()
    {
        TimeSpan ttl = _acmeOptionsMonitor.CurrentValue.Ttl;
        _logger.LogDebug("ACME challenge TTL: {Ttl}", ttl);
        return ttl;
    }
}

using System.Net;
using Makaretu.Dns;
using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DomainNames;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Handles DNS requests using Makaretu.Dns library for message parsing/serialization.
/// </summary>
public sealed class MakaretuDnsRequestHandler
{
    private readonly ILogger<MakaretuDnsRequestHandler> _logger;
    private readonly IOptionsMonitor<DnsServerOptions> _dnsServerOptionsMonitor;
    private readonly IOptionsMonitor<LegacyModeOptions> _legacyModeOptionsMonitor;
    private readonly IOptionsMonitor<EntryIpAddressMapOptions> _entryIpAddressMapOptionsMonitor;
    private readonly IAcmeTokensProvider _acmeTokensProvider;
    private readonly ISessionIpAddressCache _sessionIpAddressCache;
    private readonly ISessionRepository _sessionRepository;
    private readonly IDomainPatternMatcher _legacyMatcher;
    private readonly IDomainPatternMatcher _newMatcher;

    public MakaretuDnsRequestHandler(
        ILogger<MakaretuDnsRequestHandler> logger,
        IOptionsMonitor<DnsServerOptions> dnsServerOptionsMonitor,
        IOptionsMonitor<LegacyModeOptions> legacyModeOptionsMonitor,
        IOptionsMonitor<EntryIpAddressMapOptions> entryIpAddressMapOptionsMonitor,
        IAcmeTokensProvider acmeTokensProvider,
        ISessionIpAddressCache sessionIpAddressCache,
        ISessionRepository sessionRepository)
    {
        _logger = logger;
        _dnsServerOptionsMonitor = dnsServerOptionsMonitor;
        _legacyModeOptionsMonitor = legacyModeOptionsMonitor;
        _entryIpAddressMapOptionsMonitor = entryIpAddressMapOptionsMonitor;
        _acmeTokensProvider = acmeTokensProvider;
        _sessionIpAddressCache = sessionIpAddressCache;
        _sessionRepository = sessionRepository;
        _legacyMatcher = new LegacyDomainPatternMatcher();
        _newMatcher = new NewDomainPatternMatcher();
    }

    /// <summary>
    /// Processes a DNS query and returns a response.
    /// </summary>
    public byte[] HandleRequest(byte[] requestData)
    {
        // Sync wrapper for async method
        return HandleRequestAsync(requestData, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Processes a DNS query asynchronously and returns a response.
    /// </summary>
    private async Task<byte[]> HandleRequestAsync(byte[] requestData, CancellationToken cancellationToken)
    {
        try
        {
            Message request = new Message();
            request.Read(requestData, 0, requestData.Length);

            if (request.Questions.Count == 0)
            {
                return CreateRefusedResponse(request);
            }

            Message response = request.CreateResponse();

            foreach (Question question in request.Questions)
            {
                _logger.LogInformation("DNS query: Name={Name}, Type={Type}", question.Name, question.Type);

                // Check if this is within our authoritative zones
                if (!IsAuthoritativeForZone(question.Name.ToString()))
                {
                    _logger.LogDebug("Not authoritative for {Name}", question.Name);
                    return CreateRefusedResponse(request);
                }

                // Handle different record types
                switch (question.Type)
                {
                    case DnsType.A:
                        await HandleARecordAsync(question, response, cancellationToken);
                        break;

                    case DnsType.TXT:
                        HandleTxtRecord(question, response);
                        break;

                    default:
                        _logger.LogDebug("Unsupported query type {Type} for {Name}", question.Type, question.Name);
                        break;
                }
            }

            // If no answers were added, return NXDOMAIN
            if (response.Answers.Count == 0)
            {
                response.Status = MessageStatus.NameError;
            }

            return response.ToByteArray();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing DNS request");
            Message errorResponse = new Message { Status = MessageStatus.ServerFailure };
            return errorResponse.ToByteArray();
        }
    }

    private async Task HandleARecordAsync(Question question, Message response, CancellationToken cancellationToken)
    {
        string hostname = question.Name.ToString().TrimEnd('.');
        DnsServerOptions dnsServerOptions = _dnsServerOptionsMonitor.CurrentValue;
        LegacyModeOptions legacyModeOptions = _legacyModeOptionsMonitor.CurrentValue;

        // Try legacy pattern first
        if (legacyModeOptions.IsEnabled && _legacyMatcher.TryMatch(hostname, out DomainMatchResult legacyMatch))
        {
            if (legacyMatch.MatchType == DomainMatchType.Legacy)
            {
                _logger.LogInformation("Legacy match: GUID={Guid}, Domain={Domain}",
                    legacyMatch.LegacyGuid, legacyMatch.PublicDomain);

                ARecord aRecord = new ARecord
                {
                    Name = question.Name,
                    Address = IPAddress.Parse(legacyModeOptions.LegacyStaticIpAddress),
                    TTL = TimeSpan.FromSeconds(dnsServerOptions.ResponseTtlSeconds.LegacyA)
                };

                response.Answers.Add(aRecord);
                return;
            }
        }

        // Try new pattern
        if (_newMatcher.TryMatch(hostname, out DomainMatchResult newMatch))
        {
            if (newMatch.MatchType == DomainMatchType.New && newMatch.ProxyEntryId != null)
            {
                string sessionKey = $"{newMatch.NewAddress}-{newMatch.ProxyEntryId}";

                _logger.LogInformation("New match: Address={Address}, ProxyEntryId={ProxyEntryId}, Domain={Domain}",
                    newMatch.NewAddress, newMatch.ProxyEntryId, newMatch.PublicDomain);

                // Try cache first
                if (_sessionIpAddressCache.TryGetIpAddress(sessionKey, out string? cachedIp) && cachedIp != null)
                {
                    _logger.LogDebug("Using cached IP for session {SessionKey}: {IpAddress}", sessionKey, cachedIp);

                    ARecord aRecord = new ARecord
                    {
                        Name = question.Name,
                        Address = IPAddress.Parse(cachedIp),
                        TTL = TimeSpan.FromSeconds(dnsServerOptions.ResponseTtlSeconds.NewA)
                    };

                    response.Answers.Add(aRecord);
                    return;
                }

                // Query PostgreSQL database by hostname
                string hostnameToSearch = $"{newMatch.NewAddress}-{newMatch.ProxyEntryId}";
                Data.Session? session = await _sessionRepository.GetByHostnameAsync(hostnameToSearch, cancellationToken);

                if (session != null)
                {
                    _logger.LogInformation("Resolved IP from database for hostname {Hostname}: {IpAddress}",
                        hostnameToSearch, session.IpAddress);

                    // Cache the IP address with sliding expiration
                    _sessionIpAddressCache.SetIpAddress(sessionKey, session.IpAddress);

                    ARecord aRecord = new ARecord
                    {
                        Name = question.Name,
                        Address = IPAddress.Parse(session.IpAddress),
                        TTL = TimeSpan.FromSeconds(dnsServerOptions.ResponseTtlSeconds.NewA)
                    };

                    response.Answers.Add(aRecord);
                    return;
                }

                // Fallback to configuration map (for backwards compatibility and testing)
                EntryIpAddressMapOptions entryIpMapOptions = _entryIpAddressMapOptionsMonitor.CurrentValue;
                if (entryIpMapOptions.Map.TryGetValue(newMatch.ProxyEntryId, out string? ipAddress) && ipAddress != null)
                {
                    _logger.LogInformation("Resolved IP from configuration for proxy entry {ProxyEntryId}: {IpAddress}",
                        newMatch.ProxyEntryId, ipAddress);

                    // Cache the IP address with sliding expiration
                    _sessionIpAddressCache.SetIpAddress(sessionKey, ipAddress);

                    ARecord aRecord = new ARecord
                    {
                        Name = question.Name,
                        Address = IPAddress.Parse(ipAddress),
                        TTL = TimeSpan.FromSeconds(dnsServerOptions.ResponseTtlSeconds.NewA)
                    };

                    response.Answers.Add(aRecord);
                    return;
                }

                _logger.LogWarning("No IP mapping found for proxy entry: {ProxyEntryId}", newMatch.ProxyEntryId);
            }
        }

        // No match found - no answers added (will result in NXDOMAIN)
        _logger.LogDebug("No match found for hostname: {Hostname}", hostname);
    }

    private void HandleTxtRecord(Question question, Message response)
    {
        string hostname = question.Name.ToString().TrimEnd('.');

        // Handle ACME challenge records: _acme-challenge.tunnel4.com (exact match)
        if (hostname.Equals("_acme-challenge.tunnel4.com", StringComparison.OrdinalIgnoreCase))
        {
            IEnumerable<string> tokens = _acmeTokensProvider.GetTokens();
            List<string> tokenList = tokens.ToList();

            if (tokenList.Count > 0)
            {
                _logger.LogInformation("ACME challenge request for {Name}, returning {Count} token(s)",
                    hostname, tokenList.Count);

                TimeSpan ttl = _acmeTokensProvider.GetTtl();

                // Add one TXT record per token (Let's Encrypt requires 2 for wildcard)
                foreach (string token in tokenList)
                {
                    TXTRecord txtRecord = new TXTRecord
                    {
                        Name = question.Name,
                        Strings = new List<string> { token },
                        TTL = ttl
                    };

                    response.Answers.Add(txtRecord);
                }

                return;
            }

            _logger.LogDebug("No ACME challenge tokens configured for {Name}", hostname);
        }

        // No TXT record found - no answers added
    }

    private bool IsAuthoritativeForZone(string hostname)
    {
        hostname = hostname.TrimEnd('.');
        DnsServerOptions options = _dnsServerOptionsMonitor.CurrentValue;

        foreach (string zone in options.AuthoritativeZones)
        {
            if (hostname.Equals(zone, StringComparison.OrdinalIgnoreCase) ||
                hostname.EndsWith("." + zone, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] CreateRefusedResponse(Message request)
    {
        Message response = request.CreateResponse();
        response.Status = MessageStatus.Refused;
        return response.ToByteArray();
    }
}

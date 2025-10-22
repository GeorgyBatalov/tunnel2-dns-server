using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Protocol;
using Tunnel2.DomainNames;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// Handles DNS requests and generates appropriate responses.
/// </summary>
public class DnsRequestHandler
{
    private readonly ILogger<DnsRequestHandler> _logger;
    private readonly DnsServerOptions _dnsServerOptions;
    private readonly LegacyModeOptions _legacyModeOptions;
    private readonly EntryIpAddressMapOptions _entryIpAddressMapOptions;
    private readonly IDomainPatternMatcher _legacyMatcher;
    private readonly IDomainPatternMatcher _newMatcher;
    private readonly IAcmeTokensProvider _acmeTokensProvider;

    public DnsRequestHandler(
        ILogger<DnsRequestHandler> logger,
        IOptions<DnsServerOptions> dnsServerOptions,
        IOptions<LegacyModeOptions> legacyModeOptions,
        IOptions<EntryIpAddressMapOptions> entryIpAddressMapOptions,
        IAcmeTokensProvider acmeTokensProvider)
    {
        _logger = logger;
        _dnsServerOptions = dnsServerOptions.Value;
        _legacyModeOptions = legacyModeOptions.Value;
        _entryIpAddressMapOptions = entryIpAddressMapOptions.Value;
        _legacyMatcher = new LegacyDomainPatternMatcher();
        _newMatcher = new NewDomainPatternMatcher();
        _acmeTokensProvider = acmeTokensProvider;
    }

    /// <summary>
    /// Processes a DNS query and returns a response packet.
    /// </summary>
    public byte[] HandleRequest(byte[] requestData)
    {
        try
        {
            DnsPacket request = DnsPacket.Parse(requestData);

            if (request.Questions.Count == 0)
            {
                return CreateRefusedResponse(request);
            }

            DnsQuestion question = request.Questions[0];
            _logger.LogInformation("DNS query: Name={Name}, Type={Type}", question.Name, question.Type);

            // Check if this is within our authoritative zones
            if (!IsAuthoritativeForZone(question.Name))
            {
                _logger.LogDebug("Not authoritative for {Name}", question.Name);
                return CreateRefusedResponse(request);
            }

            // Handle A records (type 1)
            if (question.Type == 1)
            {
                return HandleARecord(request, question);
            }

            // Handle TXT records (type 16)
            if (question.Type == 16)
            {
                return HandleTxtRecord(request, question);
            }

            // Unsupported query type
            return CreateNxDomainResponse(request);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing DNS request");
            return CreateServerFailureResponse(new DnsPacket { TransactionId = 0 });
        }
    }

    private byte[] HandleARecord(DnsPacket request, DnsQuestion question)
    {
        // Try legacy pattern first
        if (_legacyModeOptions.IsEnabled && _legacyMatcher.TryMatch(question.Name, out DomainMatchResult legacyMatch))
        {
            if (legacyMatch.MatchType == DomainMatchType.Legacy)
            {
                _logger.LogInformation("Legacy match: GUID={Guid}, Domain={Domain}",
                    legacyMatch.LegacyGuid, legacyMatch.PublicDomain);

                DnsPacket response = CreateResponsePacket(request, isAuthoritative: true);
                response.Answers.Add(new DnsResourceRecord
                {
                    Name = question.Name,
                    Type = 1, // A
                    Class = 1, // IN
                    Ttl = (uint)_dnsServerOptions.ResponseTtlSeconds.LegacyA,
                    Data = _legacyModeOptions.LegacyStaticIpAddress
                });

                return response.BuildResponse();
            }
        }

        // Try new pattern
        if (_newMatcher.TryMatch(question.Name, out DomainMatchResult newMatch))
        {
            if (newMatch.MatchType == DomainMatchType.New && newMatch.ProxyEntryId != null)
            {
                _logger.LogInformation("New match: Address={Address}, ProxyEntryId={ProxyEntryId}, Domain={Domain}",
                    newMatch.NewAddress, newMatch.ProxyEntryId, newMatch.PublicDomain);

                // Look up IP address for the proxy entry
                if (_entryIpAddressMapOptions.Map.TryGetValue(newMatch.ProxyEntryId, out string? ipAddress))
                {
                    DnsPacket response = CreateResponsePacket(request, isAuthoritative: true);
                    response.Answers.Add(new DnsResourceRecord
                    {
                        Name = question.Name,
                        Type = 1, // A
                        Class = 1, // IN
                        Ttl = (uint)_dnsServerOptions.ResponseTtlSeconds.NewA,
                        Data = ipAddress
                    });

                    return response.BuildResponse();
                }

                _logger.LogWarning("No IP mapping found for proxy entry: {ProxyEntryId}", newMatch.ProxyEntryId);
            }
        }

        // No match found
        return CreateNxDomainResponse(request);
    }

    private byte[] HandleTxtRecord(DnsPacket request, DnsQuestion question)
    {
        // Handle ACME challenge records: _acme-challenge.tunnel4.com (exact match)
        if (question.Name.Equals("_acme-challenge.tunnel4.com", StringComparison.OrdinalIgnoreCase))
        {
            IEnumerable<string> tokens = _acmeTokensProvider.GetTokens();
            List<string> tokenList = tokens.ToList();

            if (tokenList.Count > 0)
            {
                _logger.LogInformation("ACME challenge request for {Name}, returning {Count} token(s)",
                    question.Name, tokenList.Count);

                DnsPacket response = CreateResponsePacket(request, isAuthoritative: true);
                TimeSpan ttl = _acmeTokensProvider.GetTtl();

                // Add one TXT record per token (Let's Encrypt requires 2 for wildcard)
                foreach (string token in tokenList)
                {
                    response.Answers.Add(new DnsResourceRecord
                    {
                        Name = question.Name,
                        Type = 16, // TXT
                        Class = 1, // IN
                        Ttl = (uint)ttl.TotalSeconds,
                        Data = token
                    });
                }

                return response.BuildResponse();
            }

            _logger.LogDebug("No ACME challenge tokens configured for {Name}", question.Name);
        }

        // No TXT record found
        return CreateNxDomainResponse(request);
    }

    private bool IsAuthoritativeForZone(string hostname)
    {
        foreach (string zone in _dnsServerOptions.AuthoritativeZones)
        {
            if (hostname.Equals(zone, StringComparison.OrdinalIgnoreCase) ||
                hostname.EndsWith("." + zone, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DnsPacket CreateResponsePacket(DnsPacket request, bool isAuthoritative)
    {
        // Flags: QR=1 (response), AA=1 if authoritative, RD=request.RD, RA=0, RCODE=0
        ushort flags = 0x8000; // QR=1
        if (isAuthoritative)
        {
            flags |= 0x0400; // AA=1
        }
        if ((request.Flags & 0x0100) != 0) // Copy RD bit
        {
            flags |= 0x0100;
        }

        return new DnsPacket
        {
            TransactionId = request.TransactionId,
            Flags = flags,
            Questions = new List<DnsQuestion>(request.Questions)
        };
    }

    private static byte[] CreateNxDomainResponse(DnsPacket request)
    {
        DnsPacket response = CreateResponsePacket(request, isAuthoritative: true);
        response.Flags |= 0x0003; // RCODE = NXDOMAIN
        return response.BuildResponse();
    }

    private static byte[] CreateRefusedResponse(DnsPacket request)
    {
        DnsPacket response = CreateResponsePacket(request, isAuthoritative: false);
        response.Flags |= 0x0005; // RCODE = REFUSED
        return response.BuildResponse();
    }

    private static byte[] CreateServerFailureResponse(DnsPacket request)
    {
        DnsPacket response = CreateResponsePacket(request, isAuthoritative: false);
        response.Flags |= 0x0002; // RCODE = SERVFAIL
        return response.BuildResponse();
    }
}

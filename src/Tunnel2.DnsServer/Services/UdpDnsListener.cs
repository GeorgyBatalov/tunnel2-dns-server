using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Tunnel2.DnsServer.Configuration;

namespace Tunnel2.DnsServer.Services;

/// <summary>
/// UDP DNS listener service that receives and responds to DNS queries.
/// </summary>
public class UdpDnsListener : BackgroundService
{
    private readonly ILogger<UdpDnsListener> _logger;
    private readonly DnsServerOptions _dnsServerOptions;
    private readonly DnsRequestHandler _requestHandler;
    private UdpClient? _udpClient;

    public UdpDnsListener(
        ILogger<UdpDnsListener> logger,
        IOptions<DnsServerOptions> dnsServerOptions,
        DnsRequestHandler requestHandler)
    {
        _logger = logger;
        _dnsServerOptions = dnsServerOptions.Value;
        _requestHandler = requestHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            IPEndPoint listenEndpoint = new IPEndPoint(
                IPAddress.Parse(_dnsServerOptions.ListenIpv4),
                _dnsServerOptions.UdpPort);

            _udpClient = new UdpClient(listenEndpoint);

            _logger.LogInformation("UDP DNS listener started on {Address}:{Port}",
                _dnsServerOptions.ListenIpv4, _dnsServerOptions.UdpPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync(stoppingToken);

                    _logger.LogDebug("Received UDP packet from {Endpoint}, size {Size} bytes",
                        result.RemoteEndPoint, result.Buffer.Length);

                    // Process request and send response (fire and forget for performance)
                    _ = Task.Run(() => ProcessRequest(result.Buffer, result.RemoteEndPoint), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error receiving UDP packet");
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Fatal error in UDP DNS listener");
            throw;
        }
    }

    private async Task ProcessRequest(byte[] requestData, IPEndPoint remoteEndpoint)
    {
        try
        {
            byte[] responseData = _requestHandler.HandleRequest(requestData);

            if (_udpClient != null)
            {
                await _udpClient.SendAsync(responseData, responseData.Length, remoteEndpoint);
                _logger.LogDebug("Sent UDP response to {Endpoint}, size {Size} bytes",
                    remoteEndpoint, responseData.Length);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing DNS request from {Endpoint}", remoteEndpoint);
        }
    }

    public override void Dispose()
    {
        _udpClient?.Dispose();
        base.Dispose();
    }
}

using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Tunnel2.DnsServer.Configuration;

namespace Tunnel2.DnsServer.MessageBroker;

/// <summary>
/// Simple factory that creates a new RabbitMQ connection and channel on each invocation.
/// </summary>
public sealed class SimpleRabbitChannelFactory : IRabbitChannelFactory
{
    private readonly RabbitOptions _rabbitOptions;

    public SimpleRabbitChannelFactory(IOptions<RabbitOptions> rabbitOptions)
    {
        _rabbitOptions = rabbitOptions.Value;
    }

    public async Task<(IConnection connection, IChannel channel)> CreateAsync(CancellationToken cancellationToken)
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitOptions.HostName,
            Port = _rabbitOptions.Port,
            VirtualHost = _rabbitOptions.VirtualHost,
            UserName = _rabbitOptions.UserName,
            Password = _rabbitOptions.Password,
            Ssl = _rabbitOptions.UseSsl ? new SslOption(_rabbitOptions.HostName, enabled: true) : new SslOption(),
            ClientProvidedName = "tunnel2-dns-server:consumer"
        };

        var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        return (connection, channel);
    }
}

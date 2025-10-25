using RabbitMQ.Client;

namespace Tunnel2.DnsServer.MessageBroker;

public interface IRabbitChannelFactory
{
    Task<(IConnection connection, IChannel channel)> CreateAsync(CancellationToken cancellationToken);
}

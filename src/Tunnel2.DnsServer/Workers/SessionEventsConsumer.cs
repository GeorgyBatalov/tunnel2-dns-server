using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.EventHandlers;
using Tunnel2.DnsServer.MessageBroker;
using Tunnel2.TunnelServer.Infrastructure.Contracts.Events;

namespace Tunnel2.DnsServer.Workers;

/// <summary>
/// Background service that consumes session events from RabbitMQ and handles HTTP session lifecycle.
/// </summary>
public sealed class SessionEventsConsumer : BackgroundService
{
    private readonly IRabbitChannelFactory _rabbitChannelFactory;
    private readonly SessionCreatedHandler _sessionCreatedHandler;
    private readonly SessionClosedHandler _sessionClosedHandler;
    private readonly RabbitOptions _rabbitOptions;
    private readonly ILogger<SessionEventsConsumer> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public SessionEventsConsumer(
        IRabbitChannelFactory rabbitChannelFactory,
        SessionCreatedHandler sessionCreatedHandler,
        SessionClosedHandler sessionClosedHandler,
        IOptions<RabbitOptions> rabbitOptions,
        ILogger<SessionEventsConsumer> logger)
    {
        _rabbitChannelFactory = rabbitChannelFactory;
        _sessionCreatedHandler = sessionCreatedHandler;
        _sessionClosedHandler = sessionClosedHandler;
        _rabbitOptions = rabbitOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionEventsConsumer starting...");

        try
        {
            // Create RabbitMQ connection and channel
            (_connection, _channel) = await _rabbitChannelFactory.CreateAsync(stoppingToken);

            // Declare exchange (topic exchange for routing keys like session.http.created)
            await _channel.ExchangeDeclareAsync(
                exchange: _rabbitOptions.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);

            // Declare queue for DNS server
            string queueName = "dns-server-session-events";
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);

            // Bind queue to exchange with routing key pattern for HTTP sessions
            // Pattern: session.http.* matches session.http.created, session.http.closed
            await _channel.QueueBindAsync(
                queue: queueName,
                exchange: _rabbitOptions.Exchange,
                routingKey: "session.http.*",
                arguments: null,
                noWait: false,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Subscribed to RabbitMQ queue {QueueName} with routing key pattern 'session.http.*'",
                queueName);

            // Set up consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                try
                {
                    await HandleMessageAsync(eventArgs, stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error handling RabbitMQ message");
                }
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true, // Auto-acknowledge messages (simplified approach)
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("SessionEventsConsumer started successfully");

            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SessionEventsConsumer stopping...");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "SessionEventsConsumer encountered an error");
            throw;
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        string routingKey = eventArgs.RoutingKey;
        string messageBody = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

        _logger.LogDebug("Received message with routing key {RoutingKey}", routingKey);

        try
        {
            // Route to appropriate handler based on routing key
            if (routingKey == "session.http.created")
            {
                var sessionCreated = JsonSerializer.Deserialize<SessionCreated>(messageBody);
                if (sessionCreated != null)
                {
                    await _sessionCreatedHandler.HandleAsync(sessionCreated, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize SessionCreated event");
                }
            }
            else if (routingKey == "session.http.closed")
            {
                var sessionClosed = JsonSerializer.Deserialize<SessionClosed>(messageBody);
                if (sessionClosed != null)
                {
                    await _sessionClosedHandler.HandleAsync(sessionClosed, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize SessionClosed event");
                }
            }
            else
            {
                _logger.LogWarning("Unknown routing key: {RoutingKey}", routingKey);
            }
        }
        catch (JsonException jsonException)
        {
            _logger.LogError(jsonException, "Failed to deserialize message for routing key {RoutingKey}", routingKey);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SessionEventsConsumer stopping...");

        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            _connection.Dispose();
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("SessionEventsConsumer stopped");
    }
}

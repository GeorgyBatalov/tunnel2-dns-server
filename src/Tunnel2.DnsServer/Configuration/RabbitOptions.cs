namespace Tunnel2.DnsServer.Configuration;

public sealed class RabbitOptions
{
    public const string SectionName = "Rabbit";
    public required string HostName { get; init; }
    public int Port { get; init; }
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string VirtualHost { get; init; }
    public required string Exchange { get; init; }
    public required bool UseSsl { get; init; }
}

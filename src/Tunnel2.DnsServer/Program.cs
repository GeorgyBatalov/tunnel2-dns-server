using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure options
builder.Services.Configure<DnsServerOptions>(builder.Configuration.GetSection("DnsServerOptions"));
builder.Services.Configure<LegacyModeOptions>(builder.Configuration.GetSection("LegacyModeOptions"));
builder.Services.Configure<EntryIpAddressMapOptions>(builder.Configuration.GetSection("EntryIpAddressMapOptions"));

// Register services
builder.Services.AddSingleton<DnsRequestHandler>();
builder.Services.AddHostedService<UdpDnsListener>();

// Configure Kestrel to listen only on health check port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

WebApplication app = builder.Build();

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", service = "tunnel2-dns-server" }));

app.Run();

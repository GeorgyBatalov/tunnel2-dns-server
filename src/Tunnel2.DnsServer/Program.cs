using Microsoft.EntityFrameworkCore;
using Tunnel2.DnsServer.Configuration;
using Tunnel2.DnsServer.Data;
using Tunnel2.DnsServer.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure options with IOptionsMonitor for hot reload support
builder.Services.Configure<DnsServerOptions>(builder.Configuration.GetSection("DnsServerOptions"));
builder.Services.Configure<LegacyModeOptions>(builder.Configuration.GetSection("LegacyModeOptions"));
builder.Services.Configure<EntryIpAddressMapOptions>(builder.Configuration.GetSection("EntryIpAddressMapOptions"));
builder.Services.Configure<AcmeOptions>(builder.Configuration.GetSection("AcmeOptions"));
builder.Services.Configure<DnsVaultOptions>(builder.Configuration.GetSection("VaultOptions"));
builder.Services.Configure<SessionCacheOptions>(builder.Configuration.GetSection("SessionCacheOptions"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("DatabaseOptions"));
builder.Services.Configure<DatabaseVaultOptions>(builder.Configuration.GetSection("DatabaseVaultOptions"));

// Register memory cache
builder.Services.AddMemoryCache(options =>
{
    SessionCacheOptions cacheOptions = builder.Configuration
        .GetSection("SessionCacheOptions")
        .Get<SessionCacheOptions>() ?? new SessionCacheOptions();

    options.SizeLimit = cacheOptions.MaxCachedSessions;
});

// Register database context with connection string provider
builder.Services.AddSingleton<IConnectionStringProvider, VaultBackedConnectionStringProvider>();
builder.Services.AddDbContextFactory<DnsServerDbContext>((serviceProvider, options) =>
{
    IConnectionStringProvider connectionStringProvider = serviceProvider.GetRequiredService<IConnectionStringProvider>();
    string connectionString = connectionStringProvider.GetConnectionString();
    options.UseNpgsql(connectionString);
});

// Register services
// Use VaultBackedAcmeTokensProvider which reads from Vault (if enabled) with fallback to appsettings.json
builder.Services.AddSingleton<IAcmeTokensProvider, VaultBackedAcmeTokensProvider>();
builder.Services.AddSingleton<ISessionIpAddressCache, SessionIpAddressCache>();
builder.Services.AddSingleton<IProxyEntryRepository, ProxyEntryRepository>();
builder.Services.AddSingleton<MakaretuDnsRequestHandler>();
builder.Services.AddHostedService<UdpDnsListener>();

// Configure Kestrel to listen only on health check port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

WebApplication app = builder.Build();

// Health check endpoint
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", service = "tunnel2-dns-server" }));

// Log startup configuration
ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Tunnel2.DnsServer started");

app.Run();

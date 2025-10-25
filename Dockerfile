# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy solution and project files
COPY Tunnel2.DnsServer.sln .
COPY src/Tunnel2.DomainNames/Tunnel2.DomainNames.csproj src/Tunnel2.DomainNames/
COPY src/Tunnel2.DnsServer/Tunnel2.DnsServer.csproj src/Tunnel2.DnsServer/
COPY tests/Tunnel2.DnsServer.Tests/Tunnel2.DnsServer.Tests.csproj tests/Tunnel2.DnsServer.Tests/

# Copy all source code
COPY src/ src/
COPY tests/ tests/

# Restore, build and publish in single RUN to preserve NuGet source config
WORKDIR /source/src/Tunnel2.DnsServer
RUN --mount=type=secret,id=github_token \
    GITHUB_TOKEN=$(cat /run/secrets/github_token) && \
    dotnet nuget add source -u GeorgyBatalov -p ${GITHUB_TOKEN} \
    --store-password-in-clear-text -n github \
    "https://nuget.pkg.github.com/GeorgyBatalov/index.json" && \
    dotnet restore && \
    dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app .

# Create non-root user but allow binding to privileged ports
RUN groupadd -r dnsserver && \
    useradd -r -g dnsserver dnsserver && \
    chown -R dnsserver:dnsserver /app

USER dnsserver

# Expose DNS ports (UDP/TCP 53) and health check port (HTTP 8080)
EXPOSE 53/udp
EXPOSE 53/tcp
EXPOSE 8080/tcp

# Set environment variables
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_EnableDiagnostics=0

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Tunnel2.DnsServer.dll"]

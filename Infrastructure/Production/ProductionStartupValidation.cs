using Microsoft.Extensions.Hosting;
using Serilog;

namespace RetailERP.Infrastructure.Production;

/// <summary>
/// Fail fast in Production when critical secrets/config are missing or unsafe.
/// Development keeps permissive defaults for local work.
/// </summary>
public static class ProductionStartupValidation
{
    private static readonly string[] JwtDevMarkers =
    [
        "RetailERP_Sprint5",
        "SuperSecretKey_2026",
        "CHANGE_ME",
        "YourProductionSecretHere"
    ];

    public static void ThrowIfInvalidForProduction(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsProduction())
            return;

        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException(
                "Production: ConnectionStrings:DefaultConnection is required. Set via environment variable or Azure/Key Vault.");

        var jwt = configuration["Jwt:SecretKey"];
        if (string.IsNullOrWhiteSpace(jwt) || jwt.Length < 32)
            throw new InvalidOperationException(
                "Production: Jwt:SecretKey must be set to a random value of at least 32 characters. " +
                "Do not use the Development sample from appsettings.json.");

        foreach (var marker in JwtDevMarkers)
        {
            if (jwt.Contains(marker, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Production: Jwt:SecretKey appears to contain a development marker ('{marker}'). Generate a new secret for production.");
        }

        var hosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(hosts) || hosts == "*")
            Log.Warning(
                "Production: AllowedHosts is '*' or empty. Set AllowedHosts to your real domain(s) for better security.");

        var allowAnonymousHealth = configuration.GetValue<bool?>("OperationalEndpoints:AllowAnonymousHealth") ?? false;
        var allowAnonymousMetrics = configuration.GetValue<bool?>("OperationalEndpoints:AllowAnonymousMetrics") ?? false;
        if (allowAnonymousHealth)
            Log.Warning("Production: OperationalEndpoints:AllowAnonymousHealth is enabled. Expose /health endpoints only behind trusted network controls.");
        if (allowAnonymousMetrics)
            Log.Warning("Production: OperationalEndpoints:AllowAnonymousMetrics is enabled. Expose /metrics only behind trusted network controls.");

        var knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()
            ?.Count(v => !string.IsNullOrWhiteSpace(v)) ?? 0;
        var knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()
            ?.Count(v => !string.IsNullOrWhiteSpace(v)) ?? 0;
        if (knownProxies == 0 && knownNetworks == 0)
            Log.Warning(
                "Production: ForwardedHeaders has no KnownProxies/KnownNetworks configured. Configure trusted proxies to prevent spoofed forwarding headers.");
    }
}

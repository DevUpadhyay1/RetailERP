using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailERP.Data;

namespace RetailERP.Tests;

/// <summary>
/// Integration test host. Uses "Testing" environment to skip the Development-only
/// DbSeeder (which calls MigrateAsync + raw SQL). Replaces EF with InMemory and
/// strips SQL/Redis health checks so no external dependencies are required.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" skips the Development-only seeder + migration branch in
        // UseRetailErpPipelineAsync, and the Production startup validation.
        builder.UseEnvironment("Testing");

        // Clear Redis so AddRetailErp() skips the synchronous probe.
        builder.UseSetting("ConnectionStrings:Redis", "");
        // Provide a dummy SQL string so AddHealthChecks.AddSqlServer() registration
        // doesn't throw; the health check itself is removed below.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\mssqllocaldb;Database=IntegrationTestDummy;Trusted_Connection=True");

        builder.ConfigureServices(services =>
        {
            // ── Replace SQL-backed DbContext with InMemory ───────────
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                          || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in dbDescriptors) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests_" + Guid.NewGuid().ToString("N")));

            // ── Replace SQL Server health check with a no-op ─────────
            var healthDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true
                          || d.ImplementationType?.FullName?.Contains("HealthCheck") == true)
                .ToList();
            foreach (var hd in healthDescriptors) services.Remove(hd);

            services.AddHealthChecks();
        });
    }
}

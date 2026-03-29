using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RetailERP.Data;

namespace RetailERP.Tests;

/// <summary>
/// Integration test host. Uses "Testing" environment to skip Development-only
/// seeding/migration branch. Replaces EF with InMemory and strips external
/// health checks so no external dependencies are required.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Clear Redis so AddRetailErp() skips the synchronous probe.
        builder.UseSetting("ConnectionStrings:Redis", "");
        // Provide a dummy SQL string so health-check registration does not throw;
        // SQL health checks are removed below.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\mssqllocaldb;Database=IntegrationTestDummy;Trusted_Connection=True");
        builder.UseSetting("Jwt:SecretKey", "IntegrationTestDummyKeyThatIsAtLeast32Bytes!");

        builder.ConfigureServices(services =>
        {
            // Replace SQL-backed DbContext with InMemory.
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var descriptor in dbDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests_" + Guid.NewGuid().ToString("N")));

            // Replace external health checks with no-op health check registration.
            var healthDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true
                         || d.ImplementationType?.FullName?.Contains("HealthCheck") == true)
                .ToList();
            foreach (var descriptor in healthDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddHealthChecks();

            // Keep tests self-contained: avoid file-system key ring writes.
            var dataProtectionDescriptors = services
                .Where(d => d.ServiceType == typeof(IDataProtectionProvider))
                .ToList();
            foreach (var descriptor in dataProtectionDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace RetailERP.Tests;

/// <summary>
/// Derived factory that adds Swagger middleware to the Testing host.
/// The base <see cref="CustomWebApplicationFactory"/> uses "Testing" environment
/// (which skips the Development-only DbSeeder + migration path).
/// This factory injects Swagger via an <see cref="IStartupFilter"/> so that
/// the /swagger/v1/swagger.json endpoint is available for integration tests
/// without altering the environment or the rest of the pipeline.
/// </summary>
public class DevelopmentWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Apply all base overrides (Testing env, InMemory DB, health stubs).
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Inject Swagger middleware at the start of the pipeline.
            services.AddTransient<IStartupFilter, SwaggerStartupFilter>();
        });
    }

    /// <summary>
    /// Startup filter that prepends UseSwagger + UseSwaggerUI to the
    /// existing pipeline without replacing it.
    /// </summary>
    private sealed class SwaggerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RetailERP API v1");
                    c.RoutePrefix = "swagger";
                });
                next(app);
            };
        }
    }
}

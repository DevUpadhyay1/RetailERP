using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RetailERP.Data.Seed;
using RetailERP.Hubs;
using Serilog;

namespace RetailERP.Infrastructure;

/// <summary>
/// HTTP pipeline configuration (extracted from <see cref="Program"/>).
/// </summary>
public static class WebApplicationExtensions
{
    public static async Task UseRetailErpPipelineAsync(this WebApplication app)
    {
        // Must run first when behind reverse proxy (TLS termination at nginx / load balancer).
        if (!app.Environment.IsDevelopment())
            app.UseForwardedHeaders();

        // Correlation ID: must run before Serilog request logging so the ID is
        // available in LogContext and diagnostic-context enrichment below.
        app.UseMiddleware<CorrelationIdMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "RetailERP API v1");
                c.RoutePrefix = "swagger";
            });

            using var scope = app.Services.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            await seeder.SeedAsync();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseStatusCodePagesWithReExecute("/Home/HttpStatus/{0}");
            app.UseHsts();
        }

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
                diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name ?? "anonymous");
                diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            };
        });

        var appMetrics = app.Services.GetRequiredService<AppMetricsService>();
        app.Use(async (context, next) =>
        {
            var startedAt = DateTime.UtcNow;
            using var _ = appMetrics.BeginRequest();
            await next();
            var durationMs = (long)Math.Max(0, (DateTime.UtcNow - startedAt).TotalMilliseconds);
            appMetrics.TrackCompletedRequest(context, durationMs);
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        // Development: relax CSP to allow Browser Link / Hot Reload on random localhost ports.
        // Production: strict CSP that only allows known origins.
        var isDev = app.Environment.IsDevelopment();

        app.Use(async (context, next) =>
        {
            var h = context.Response.Headers;
            h.TryAdd("X-Content-Type-Options", "nosniff");
            h.TryAdd("X-Frame-Options", "DENY");
            h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            h.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            h.TryAdd("X-XSS-Protection", "1; mode=block");

            var connectSrc = isDev
                ? "connect-src 'self' wss: ws: http://localhost:* https://cdn.jsdelivr.net https://api.razorpay.com https://lumberjack.razorpay.com https://cloudflareinsights.com; "
                : "connect-src 'self' wss: ws: https://cdn.jsdelivr.net https://api.razorpay.com https://lumberjack.razorpay.com https://cloudflareinsights.com; ";

            var scriptSrc = isDev
                ? "script-src 'self' 'unsafe-inline' 'unsafe-eval' http://localhost:* https://cdn.jsdelivr.net https://checkout.razorpay.com https://api.razorpay.com https://static.cloudflareinsights.com; "
                : "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://checkout.razorpay.com https://api.razorpay.com https://static.cloudflareinsights.com; ";

            h.TryAdd("Content-Security-Policy",
                "default-src 'self'; " +
                scriptSrc +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
                "font-src 'self' https://cdn.jsdelivr.net https://fonts.gstatic.com data:; " +
                "img-src 'self' data: https://rzp.io https://lumberjack.razorpay.com; " +
                connectSrc +
                "frame-src 'self' https://api.razorpay.com https://checkout.razorpay.com; " +
                "object-src 'self' blob:; " +
                "frame-ancestors 'none';");
            await next();
        });

        // Ensure HTML responses explicitly declare UTF-8 for Lighthouse/browser compatibility checks.
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var contentType = context.Response.ContentType;
                if (!string.IsNullOrWhiteSpace(contentType)
                    && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                    && !contentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = $"{contentType}; charset=utf-8";
                }

                return Task.CompletedTask;
            });

            await next();
        });

        app.UseRequestLocalization();

        app.UseRouting();
        app.UseCors("ApiCors");

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Use(async (context, next) =>
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
                var tokens = antiforgery.GetAndStoreTokens(context);
                context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
                    new CookieOptions
                    {
                        HttpOnly = false,
                        SameSite = SameSiteMode.Strict,
                        Secure = context.Request.IsHttps
                    });
            }

            await next();
        });

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.MapRazorPages();

        app.MapHub<RetailHub>("/hubs/retail");

        var allowAnonymousHealthEndpoints = app.Configuration.GetValue<bool?>("OperationalEndpoints:AllowAnonymousHealth")
            ?? app.Environment.IsDevelopment();
        var allowAnonymousMetricsEndpoint = app.Configuration.GetValue<bool?>("OperationalEndpoints:AllowAnonymousMetrics")
            ?? app.Environment.IsDevelopment();

        if (!allowAnonymousHealthEndpoints || !allowAnonymousMetricsEndpoint)
        {
            Log.Information(
                "Operational endpoints are protected. AllowAnonymousHealth={AllowAnonymousHealth}, AllowAnonymousMetrics={AllowAnonymousMetrics}",
                allowAnonymousHealthEndpoints,
                allowAnonymousMetricsEndpoint);
        }

        var livenessEndpoint = app.MapHealthChecks("/health");
        if (allowAnonymousHealthEndpoints)
            livenessEndpoint.AllowAnonymous();
        else
            livenessEndpoint.RequireAuthorization();

        // Kubernetes-style readiness: SQL (+ Redis when enabled), tagged "ready" in AddHealthChecks.
        var readinessEndpoint = app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(
                        e => e.Key,
                        e => new
                        {
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description,
                            durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 2)
                        })
                });
            }
        });
        if (allowAnonymousHealthEndpoints)
            readinessEndpoint.AllowAnonymous();
        else
            readinessEndpoint.RequireAuthorization();

        var metricsEndpoint = app.MapGet("/metrics", () =>
        {
            var payload = appMetrics.RenderPrometheus();
            return Results.Text(payload, "text/plain; version=0.0.4; charset=utf-8");
        });
        if (allowAnonymousMetricsEndpoint)
            metricsEndpoint.AllowAnonymous();
        else
            metricsEndpoint.RequireAuthorization();
    }
}

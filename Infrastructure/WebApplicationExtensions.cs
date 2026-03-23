using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
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
            app.UseHsts();
        }

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
                diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name ?? "anonymous");
            };
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.Use(async (context, next) =>
        {
            var h = context.Response.Headers;
            h.TryAdd("X-Content-Type-Options", "nosniff");
            h.TryAdd("X-Frame-Options", "DENY");
            h.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            h.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            h.TryAdd("X-XSS-Protection", "1; mode=block");
            h.TryAdd("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://checkout.razorpay.com https://api.razorpay.com; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "font-src 'self' https://cdn.jsdelivr.net data:; " +
                "img-src 'self' data: https://rzp.io https://lumberjack.razorpay.com; " +
                "connect-src 'self' wss: ws: https://cdn.jsdelivr.net https://api.razorpay.com https://lumberjack.razorpay.com; " +
                "frame-src https://api.razorpay.com https://checkout.razorpay.com; " +
                "frame-ancestors 'none';");
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

        app.MapHealthChecks("/health").AllowAnonymous();
    }
}

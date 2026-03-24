using System.Diagnostics;
using Serilog.Context;

namespace RetailERP.Infrastructure;

/// <summary>
/// Injects a correlation ID into every request for end-to-end traceability.
/// Reads "X-Correlation-Id" from the incoming request header if present;
/// otherwise generates a new GUID.
/// Design choice: we overwrite HttpContext.TraceIdentifier so that Kestrel's
/// built-in logging, Serilog enrichers, and any code using TraceIdentifier
/// all share the same value — single source of truth.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    /// <summary>Typed key for HttpContext.Items to avoid magic strings in consumers.</summary>
    public static readonly object CorrelationIdKey = new();

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Resolve correlation ID: prefer incoming header, else generate.
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("D");

        // 2. Store on HttpContext for downstream access.
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdKey] = correlationId;

        // 3. Always echo back in response header.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(HeaderName, correlationId);
            return Task.CompletedTask;
        });

        // 4. Push into Serilog LogContext so every log line includes it.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            // 5. (Optional) Tag current Activity for OpenTelemetry compatibility.
            Activity.Current?.SetTag("correlation.id", correlationId);

            await _next(context);
        }
    }
}

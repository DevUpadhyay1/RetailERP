using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Routing;

namespace RetailERP.Infrastructure;

/// <summary>
/// Lightweight in-process metrics for operational visibility.
/// Exposed via /metrics in Prometheus text format.
/// </summary>
public sealed class AppMetricsService
{
    private long _requestsTotal;
    private long _errorsTotal;
    private long _activeRequests;
    private long _requestDurationMsTotal;

    private readonly ConcurrentDictionary<string, RouteMetric> _byRoute = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable BeginRequest()
    {
        Interlocked.Increment(ref _activeRequests);
        return new RequestScope(this);
    }

    public void TrackCompletedRequest(HttpContext context, long durationMs)
    {
        Interlocked.Increment(ref _requestsTotal);
        Interlocked.Add(ref _requestDurationMsTotal, durationMs);

        var statusCode = context.Response.StatusCode;
        if (statusCode >= 500)
            Interlocked.Increment(ref _errorsTotal);

        var route = ResolveRouteLabel(context);
        var statusClass = $"{Math.Clamp(statusCode / 100, 0, 9)}xx";
        var key = $"{route}|{statusClass}";
        var metric = _byRoute.GetOrAdd(key, _ => new RouteMetric(route, statusClass));

        Interlocked.Increment(ref metric.Count);
        Interlocked.Add(ref metric.DurationMsTotal, durationMs);
        if (statusCode >= 500)
            Interlocked.Increment(ref metric.Errors);
    }

    public string RenderPrometheus()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# HELP retailerp_requests_total Total HTTP requests handled.");
        sb.AppendLine("# TYPE retailerp_requests_total counter");
        sb.AppendLine($"retailerp_requests_total {Volatile.Read(ref _requestsTotal)}");

        sb.AppendLine("# HELP retailerp_errors_total Total HTTP 5xx responses.");
        sb.AppendLine("# TYPE retailerp_errors_total counter");
        sb.AppendLine($"retailerp_errors_total {Volatile.Read(ref _errorsTotal)}");

        sb.AppendLine("# HELP retailerp_active_requests Current in-flight HTTP requests.");
        sb.AppendLine("# TYPE retailerp_active_requests gauge");
        sb.AppendLine($"retailerp_active_requests {Volatile.Read(ref _activeRequests)}");

        sb.AppendLine("# HELP retailerp_request_duration_ms_total Cumulative request duration in milliseconds.");
        sb.AppendLine("# TYPE retailerp_request_duration_ms_total counter");
        sb.AppendLine($"retailerp_request_duration_ms_total {Volatile.Read(ref _requestDurationMsTotal)}");

        sb.AppendLine("# HELP retailerp_route_requests_total Requests grouped by route and status class.");
        sb.AppendLine("# TYPE retailerp_route_requests_total counter");
        foreach (var metric in _byRoute.Values.OrderBy(v => v.Route).ThenBy(v => v.StatusClass))
        {
            sb.Append("retailerp_route_requests_total{route=\"")
                .Append(EscapeLabel(metric.Route))
                .Append("\",status_class=\"")
                .Append(metric.StatusClass)
                .Append("\"} ")
                .AppendLine(Volatile.Read(ref metric.Count).ToString());
        }

        return sb.ToString();
    }

    private static string ResolveRouteLabel(HttpContext context)
    {
        var endpoint = context.GetEndpoint() as RouteEndpoint;
        var routePattern = endpoint?.RoutePattern.RawText;
        if (!string.IsNullOrWhiteSpace(routePattern))
            return routePattern!;

        var path = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        return path!;
    }

    private static string EscapeLabel(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed class RequestScope : IDisposable
    {
        private readonly AppMetricsService _owner;
        private int _disposed;

        public RequestScope(AppMetricsService owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref _owner._activeRequests);
        }
    }

    private sealed class RouteMetric
    {
        public RouteMetric(string route, string statusClass)
        {
            Route = route;
            StatusClass = statusClass;
        }

        public string Route { get; }
        public string StatusClass { get; }
        public long Count;
        public long Errors;
        public long DurationMsTotal;
    }
}

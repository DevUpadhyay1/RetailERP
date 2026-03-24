using Serilog;
using Serilog.Events;

namespace RetailERP.Infrastructure;

/// <summary>
/// Central Serilog configuration so <see cref="Program"/> stays minimal.
/// </summary>
public static class SerilogBootstrap
{
    public static LoggerConfiguration CreateLoggerConfiguration()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: "Logs/retailerp-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 50 * 1024 * 1024, // 50 MB max per file
                rollOnFileSizeLimit: true,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} | {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    public static Serilog.Core.Logger CreateLogger() => CreateLoggerConfiguration().CreateLogger();
}

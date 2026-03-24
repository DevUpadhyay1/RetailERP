using RetailERP.Infrastructure;
using Serilog;

// Serilog bootstrap (catches startup exceptions before host is built)
Log.Logger = SerilogBootstrap.CreateLogger();

try
{
    Log.Information("RetailERP starting up…");

    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.AddRetailErp();

    var app = builder.Build();
    await app.UseRetailErpPipelineAsync();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "RetailERP terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make the auto-generated Program class accessible to WebApplicationFactory<Program> in test projects.
public partial class Program { }

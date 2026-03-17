using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using RetailERP.Data;
using RetailERP.Hubs;

namespace RetailERP.Services.BackgroundJobs;

/// <summary>
/// Sprint 9: Background worker that auto-generates preliminary EOD reports
/// at a configurable time (default: 10:00 PM IST) for all stores that had activity today.
/// The report stays in "Open" status until a manager closes it with actual cash count.
/// </summary>
public sealed class EodAutoWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RetailHub> _hub;
    private readonly ILogger<EodAutoWorker> _log;

    // IST = UTC + 5:30
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
    private static readonly TimeOnly TriggerTime = new(22, 0); // 10:00 PM IST

    public EodAutoWorker(IServiceScopeFactory scopeFactory,
                         IHubContext<RetailHub> hub,
                         ILogger<EodAutoWorker> log)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Sprint 9: EodAutoWorker started (trigger: {Time} IST)", TriggerTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowIst = DateTime.UtcNow + IstOffset;
            var todayTrigger = nowIst.Date + TriggerTime.ToTimeSpan();

            // If we've already passed today's trigger, schedule for tomorrow
            if (nowIst >= todayTrigger)
                todayTrigger = todayTrigger.AddDays(1);

            var delay = todayTrigger - nowIst;
            _log.LogInformation("EodAutoWorker: next run in {Delay}", delay);

            await Task.Delay(delay, stoppingToken);

            try
            {
                await GenerateEodReportsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "EodAutoWorker: error generating EOD reports");
            }
        }
    }

    private async Task GenerateEodReportsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eodService = scope.ServiceProvider.GetRequiredService<EodService>();

        var today = DateTime.Today;

        // Find all stores that had completed POS bills today
        var activeStoreIds = await db.PosBills
            .Where(b => b.Status == 2 && b.BillDate == today)
            .Select(b => b.StoreId)
            .Distinct()
            .ToListAsync(ct);

        if (activeStoreIds.Count == 0)
        {
            _log.LogInformation("EodAutoWorker: no active stores today, skipping");
            return;
        }

        _log.LogInformation("EodAutoWorker: generating reports for {Count} stores", activeStoreIds.Count);

        foreach (var storeId in activeStoreIds)
        {
            try
            {
                // Check if an EOD report already exists for this store today
                var exists = await db.EodReports
                    .AnyAsync(r => r.StoreId == storeId && r.ReportDate == today, ct);

                if (exists) continue;

                await eodService.GenerateReportAsync(storeId, today, 0);

                // Get store's company for SignalR group
                var companyId = await db.Stores
                    .Where(s => s.StoreId == storeId)
                    .Select(s => s.CompanyId)
                    .FirstOrDefaultAsync(ct);

                if (companyId.HasValue)
                {
                    await _hub.Clients.Group($"company-{companyId}")
                        .SendAsync("EodReportGenerated", new
                        {
                            storeId,
                            reportDate = today.ToString("yyyy-MM-dd"),
                            timestamp = DateTime.UtcNow
                        }, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "EodAutoWorker: failed for store {StoreId}", storeId);
            }
        }
    }
}

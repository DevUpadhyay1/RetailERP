using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using RetailERP.Data;
using RetailERP.Hubs;
using RetailERP.Services;

namespace RetailERP.Services.BackgroundJobs;

/// <summary>
/// Sprint 9: Periodic worker that checks for low-stock items (below ReorderLevel)
/// and pushes real-time alerts via SignalR + queues email notifications to admins.
/// Runs every 15 minutes.
/// </summary>
public sealed class StockAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<RetailHub> _hub;
    private readonly EmailQueueService _emailQueue;
    private readonly ILogger<StockAlertWorker> _log;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public StockAlertWorker(IServiceScopeFactory scopeFactory,
                            IHubContext<RetailHub> hub,
                            EmailQueueService emailQueue,
                            ILogger<StockAlertWorker> log)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _emailQueue = emailQueue;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Sprint 9: StockAlertWorker started (interval: {Interval})", Interval);

        // Wait 30s after startup to let the app fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckStockLevelsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "StockAlertWorker: error during stock check");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckStockLevelsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Same rule as Items/LowStock, dashboard widgets, and Background Jobs monitor (includes 0 on-hand with no Stock rows)
        var lowStockItems = await LowStockReporting.Query(db).ToListAsync(ct);

        if (lowStockItems.Count == 0) return;

        _log.LogInformation("StockAlertWorker: {Count} items below reorder level", lowStockItems.Count);

        // Group by company so we broadcast to the right SignalR group
        var byCompany = lowStockItems.GroupBy(x => x.CompanyId);

        foreach (var group in byCompany)
        {
            var companyId = group.Key?.ToString() ?? "unknown";
            var alerts = group.Select(x => new
            {
                x.ItemId,
                x.Name,
                x.SKU,
                x.ReorderLevel,
                CurrentStock = x.OnHand
            }).ToList();

            // Push real-time alert to all connected dashboard clients in this company
            await _hub.Clients.Group($"company-{companyId}")
                .SendAsync("StockAlert", new
                {
                    count = alerts.Count,
                    items = alerts,
                    timestamp = DateTime.UtcNow
                }, ct);
        }

        // Queue summary email to admins (one per company)
        foreach (var group in byCompany)
        {
            var companyId = group.Key;
            if (companyId is null) continue;

            var adminEmails = await db.Users
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .Join(db.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                .Join(db.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r })
                .Where(x => x.r.Name == "Admin" || x.r.Name == "Manager")
                .Select(x => x.u.Email!)
                .Where(e => e != null)
                .Distinct()
                .ToListAsync(ct);

            if (adminEmails.Count == 0) continue;

            var rows = string.Join("", group.Select(x =>
                $"<tr><td>{x.SKU}</td><td>{x.Name}</td><td style='color:red;font-weight:bold'>{x.OnHand:N0}</td><td>{x.ReorderLevel}</td></tr>"));

            var html = $@"
                <h3>Low Stock Alert — {group.Count()} Items Below Reorder Level</h3>
                <table border='1' cellpadding='6' cellspacing='0' style='border-collapse:collapse'>
                    <tr style='background:#f8d7da'><th>SKU</th><th>Item</th><th>Current Stock</th><th>Reorder Level</th></tr>
                    {rows}
                </table>
                <p style='color:#666;font-size:12px'>This is an automated alert from RetailERP. Checked at {DateTime.UtcNow:u}</p>";

            foreach (var email in adminEmails)
            {
                await _emailQueue.EnqueueAsync(email, $"[RetailERP] Low Stock Alert — {group.Count()} Items", html, ct);
            }
        }
    }
}

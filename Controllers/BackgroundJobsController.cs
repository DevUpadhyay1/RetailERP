using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Controllers;

/// <summary>
/// Sprint 9: Monitoring dashboard for background workers.
/// Shows status of email queue, sync queue, stock alerts, and EOD auto-generation.
/// </summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class BackgroundJobsController : Controller
{
    private readonly ApplicationDbContext _db;

    public BackgroundJobsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var syncPending = await _db.SyncLogs.CountAsync(s => s.Status == 1);
        var syncSynced = await _db.SyncLogs.CountAsync(s => s.Status == 2);
        var syncConflict = await _db.SyncLogs.CountAsync(s => s.Status == 3);

        var todayEod = await _db.EodReports.CountAsync(r => r.ReportDate == DateTime.Today);

        var lowStockCount = await _db.Items
            .Where(i => i.IsActive && i.ReorderLevel > 0)
            .CountAsync(i => _db.Stocks
                .Where(s => s.ItemId == i.ItemId)
                .Sum(s => (decimal?)s.Quantity) < i.ReorderLevel);

        ViewBag.SyncPending = syncPending;
        ViewBag.SyncSynced = syncSynced;
        ViewBag.SyncConflict = syncConflict;
        ViewBag.TodayEodReports = todayEod;
        ViewBag.LowStockItems = lowStockCount;

        return View();
    }
}

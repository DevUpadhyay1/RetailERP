using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin")]
public class SyncController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SyncService _sync;

    public SyncController(ApplicationDbContext db, SyncService sync)
    {
        _db = db;
        _sync = sync;
    }

    // ── Dashboard ──
    [HttpGet]
    public async Task<IActionResult> Index(byte? status, string? deviceId, string sort = "QueuedAtUtc", string dir = "desc", int page = 1)
    {
        const int pageSize = 30;
        var query = _db.SyncLogs.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(deviceId))
            query = query.Where(s => s.DeviceId == deviceId);

        query = sort switch
        {
            "DeviceId" => dir == "asc" ? query.OrderBy(s => s.DeviceId) : query.OrderByDescending(s => s.DeviceId),
            "EntityType" => dir == "asc" ? query.OrderBy(s => s.EntityType) : query.OrderByDescending(s => s.EntityType),
            "Status" => dir == "asc" ? query.OrderBy(s => s.Status) : query.OrderByDescending(s => s.Status),
            _ => dir == "asc" ? query.OrderBy(s => s.QueuedAtUtc) : query.OrderByDescending(s => s.QueuedAtUtc),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var stats = await _sync.GetStatsAsync();

        ViewBag.Stats = stats;
        ViewBag.Status = status;
        ViewBag.DeviceId = deviceId;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(items);
    }

    // ── Details ──
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var log = await _db.SyncLogs.AsNoTracking().FirstOrDefaultAsync(s => s.SyncLogId == id);
        if (log is null) return NotFound();
        return View(log);
    }

    // ── Resolve conflict ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid syncLogId, string resolution)
    {
        try
        {
            await _sync.ResolveConflictAsync(syncLogId, resolution);
            TempData["Success"] = "Conflict resolved.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = syncLogId });
    }

    // ── Retry: re-mark as pending ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid syncLogId)
    {
        var log = await _db.SyncLogs.FirstOrDefaultAsync(s => s.SyncLogId == syncLogId);
        if (log is null) return NotFound();

        log.Status = 1; // Pending
        log.ConflictDetails = null;
        log.Resolution = null;
        log.ResolvedAtUtc = null;
        log.SyncedAtUtc = null;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Queued for retry.";
        return RedirectToAction(nameof(Index));
    }

    // ── API: Queue an offline change (called by POS terminal) ──
    [HttpPost, IgnoreAntiforgeryToken]
    [AllowAnonymous] // POS terminals authenticate via device ID
    public async Task<IActionResult> QueueChange([FromBody] QueueChangeReq req)
    {
        try
        {
            var id = await _sync.QueueChangeAsync(req.DeviceId, req.EntityType, req.EntityId, req.Action, req.Payload);
            return Json(new { success = true, syncLogId = id });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── API: Process all pending sync entries ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessAll()
    {
        var pending = await _sync.GetPendingAsync();
        int synced = 0, conflicted = 0;

        foreach (var log in pending)
        {
            try
            {
                // In a real system, this would apply the payload changes to the main DB.
                // For now, we auto-mark as synced.
                await _sync.MarkSyncedAsync(log.SyncLogId);
                synced++;
            }
            catch
            {
                await _sync.MarkConflictAsync(log.SyncLogId, "Auto-sync failed.");
                conflicted++;
            }
        }

        TempData["Success"] = $"Processed {synced + conflicted} entries: {synced} synced, {conflicted} conflicts.";
        return RedirectToAction(nameof(Index));
    }

    public class QueueChangeReq
    {
        public string DeviceId { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "Create";
        public object? Payload { get; set; }
    }
}

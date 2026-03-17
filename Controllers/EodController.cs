using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class EodController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly EodService _eod;
    private readonly UserManager<ApplicationUser> _userManager;

    public EodController(ApplicationDbContext db, EodService eod, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _eod = eod;
        _userManager = userManager;
    }

    // ── Report list ──
    [HttpGet]
    public async Task<IActionResult> Index(Guid? storeId, string sort = "ReportDate", string dir = "desc", int page = 1)
    {
        const int pageSize = 20;
        var query = _db.EodReports.AsNoTracking().Include(r => r.Store).AsQueryable();

        if (storeId.HasValue)
            query = query.Where(r => r.StoreId == storeId.Value);

        query = sort switch
        {
            "Store" => dir == "asc" ? query.OrderBy(r => r.Store!.Name) : query.OrderByDescending(r => r.Store!.Name),
            "NetSales" => dir == "asc" ? query.OrderBy(r => r.NetSales) : query.OrderByDescending(r => r.NetSales),
            "Status" => dir == "asc" ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status),
            _ => dir == "asc" ? query.OrderBy(r => r.ReportDate) : query.OrderByDescending(r => r.ReportDate),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.StoreId = storeId;
        ViewBag.Stores = new SelectList(await _db.Stores.OrderBy(s => s.Name).ToListAsync(), "StoreId", "Name", storeId);
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(items);
    }

    // ── Generate report ──
    [HttpGet]
    public async Task<IActionResult> Generate()
    {
        ViewBag.Stores = new SelectList(await _db.Stores.OrderBy(s => s.Name).ToListAsync(), "StoreId", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(Guid storeId, DateTime reportDate, decimal openingCash)
    {
        try
        {
            var report = await _eod.GenerateReportAsync(storeId, reportDate, openingCash);
            TempData["Success"] = "EOD report generated.";
            return RedirectToAction(nameof(Details), new { id = report.EodReportId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            ViewBag.Stores = new SelectList(await _db.Stores.OrderBy(s => s.Name).ToListAsync(), "StoreId", "Name", storeId);
            return View();
        }
    }

    // ── Details ──
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var report = await _db.EodReports.AsNoTracking()
            .Include(r => r.Store)
            .Include(r => r.ClosedByUser)
            .FirstOrDefaultAsync(r => r.EodReportId == id);

        if (report is null) return NotFound();
        return View(report);
    }

    // ── Close day ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid eodReportId, decimal actualCash, string? notes)
    {
        try
        {
            var userId = Guid.Parse(_userManager.GetUserId(User)!);
            await _eod.CloseAsync(eodReportId, actualCash, notes, userId);
            TempData["Success"] = "Day closed successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = eodReportId });
    }

    // ── Print-friendly report ──
    [HttpGet]
    public async Task<IActionResult> Print(Guid id)
    {
        var report = await _db.EodReports.AsNoTracking()
            .Include(r => r.Store)
            .Include(r => r.ClosedByUser)
            .FirstOrDefaultAsync(r => r.EodReportId == id);

        if (report is null) return NotFound();
        return View(report);
    }
}

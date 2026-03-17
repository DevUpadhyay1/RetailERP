using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Finance")]
public class EInvoicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly EInvoiceService _einv;

    public EInvoicesController(ApplicationDbContext db, EInvoiceService einv)
    {
        _db = db;
        _einv = einv;
    }

    // ═══════════════════════════════════════════════════════════
    // Index — list all E-Invoices
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Index(byte? status)
    {
        var q = _db.EInvoices
            .AsNoTracking()
            .Include(e => e.PosBill)
            .Include(e => e.Invoice)
            .AsQueryable();

        if (status.HasValue)
            q = q.Where(e => e.Status == status.Value);

        var list = await q.OrderByDescending(e => e.GeneratedAtUtc).Take(200).ToListAsync();
        ViewBag.StatusFilter = status;
        return View(list);
    }

    // ═══════════════════════════════════════════════════════════
    // Details — single E-Invoice with JSON payload and QR
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> Details(Guid id)
    {
        var einv = await _db.EInvoices
            .AsNoTracking()
            .Include(e => e.PosBill).ThenInclude(b => b!.Customer)
            .Include(e => e.PosBill).ThenInclude(b => b!.Store)
            .Include(e => e.Invoice).ThenInclude(i => i!.Customer)
            .FirstOrDefaultAsync(e => e.EInvoiceId == id);

        if (einv is null) return NotFound();
        return View(einv);
    }

    // ═══════════════════════════════════════════════════════════
    // Generate — pick a bill/invoice and generate E-Invoice
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Generate()
    {
        // Show completed POS bills and posted invoices that don't yet have an active E-Invoice
        var existingBillIds = await _db.EInvoices.Where(e => e.Status == 1 && e.PosBillId != null)
            .Select(e => e.PosBillId!.Value).ToListAsync();
        var existingInvIds = await _db.EInvoices.Where(e => e.Status == 1 && e.InvoiceId != null)
            .Select(e => e.InvoiceId!.Value).ToListAsync();

        var bills = await _db.PosBills.AsNoTracking()
            .Include(b => b.Customer)
            .Where(b => b.Status == 2 && !existingBillIds.Contains(b.PosBillId))
            .OrderByDescending(b => b.BillDate)
            .Take(50)
            .ToListAsync();

        var invoices = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.Status == 2 && !existingInvIds.Contains(i.InvoiceId))
            .OrderByDescending(i => i.InvoiceDate)
            .Take(50)
            .ToListAsync();

        return View(new GenerateEInvoiceVm { Bills = bills, Invoices = invoices });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateForBill(Guid posBillId)
    {
        try
        {
            var einv = await _einv.GenerateForPosBillAsync(posBillId);
            TempData["Success"] = $"E-Invoice generated. IRN: {einv.Irn[..16]}…";
            return RedirectToAction(nameof(Details), new { id = einv.EInvoiceId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Generate));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateForInvoice(Guid invoiceId)
    {
        try
        {
            var einv = await _einv.GenerateForInvoiceAsync(invoiceId);
            TempData["Success"] = $"E-Invoice generated. IRN: {einv.Irn[..16]}…";
            return RedirectToAction(nameof(Details), new { id = einv.EInvoiceId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Generate));
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Cancel
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string reason)
    {
        try
        {
            await _einv.CancelAsync(id, reason);
            TempData["Success"] = "E-Invoice cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // ═══════════════════════════════════════════════════════════
    // E-Way Bills
    // ═══════════════════════════════════════════════════════════

    public async Task<IActionResult> EWayBills(byte? status)
    {
        var q = _db.EWayBills
            .AsNoTracking()
            .Include(e => e.PosBill)
            .Include(e => e.Invoice)
            .AsQueryable();

        if (status.HasValue)
            q = q.Where(e => e.Status == status.Value);

        var list = await q.OrderByDescending(e => e.GeneratedAtUtc).Take(200).ToListAsync();
        ViewBag.StatusFilter = status;
        return View(list);
    }

    public async Task<IActionResult> EWayBillDetails(Guid id)
    {
        var ewb = await _db.EWayBills
            .AsNoTracking()
            .Include(e => e.PosBill).ThenInclude(b => b!.Store)
            .Include(e => e.Invoice)
            .FirstOrDefaultAsync(e => e.EWayBillId == id);

        if (ewb is null) return NotFound();
        return View(ewb);
    }

    [HttpGet]
    public async Task<IActionResult> GenerateEWayBill(Guid posBillId)
    {
        var bill = await _db.PosBills.AsNoTracking()
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.PosBillId == posBillId);
        if (bill is null) return NotFound();
        ViewBag.Bill = bill;
        return View(new EWayBillInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateEWayBill(Guid posBillId, EWayBillInput input)
    {
        try
        {
            var ewb = await _einv.GenerateEWayBillAsync(posBillId, input);
            TempData["Success"] = $"E-Way Bill generated: {ewb.EwbNo}";
            return RedirectToAction(nameof(EWayBillDetails), new { id = ewb.EWayBillId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(EWayBills));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEWayBill(Guid id, string reason)
    {
        try
        {
            await _einv.CancelEWayBillAsync(id, reason);
            TempData["Success"] = "E-Way Bill cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(EWayBillDetails), new { id });
    }
}

public sealed class GenerateEInvoiceVm
{
    public List<RetailERP.Data.Entities.PosBill> Bills { get; set; } = new();
    public List<RetailERP.Data.Entities.Invoice> Invoices { get; set; } = new();
}

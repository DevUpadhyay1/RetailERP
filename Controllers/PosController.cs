using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using RetailERP.Services;
using System.Security.Claims;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier")]
[EnableRateLimiting("Pos")]
public class PosController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PosBillingService _pos;
    private readonly PromotionService _promo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ReceiptPdfService _receiptPdf;

    public PosController(ApplicationDbContext db, PosBillingService pos, PromotionService promo,
        UserManager<ApplicationUser> userManager, ReceiptPdfService receiptPdf)
    {
        _db = db;
        _pos = pos;
        _promo = promo;
        _userManager = userManager;
        _receiptPdf = receiptPdf;
    }

    // ────────────────────────────────────────────────────────
    // POS Bill list (history)
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index(string? q, byte? status, string sort = "date", string dir = "desc", int page = 1, int pageSize = 20)
    {
        q = (q ?? "").Trim();
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        ViewData["q"] = q;
        ViewData["status"] = status;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.PosBills
            .AsNoTracking()
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .Include(b => b.CashierUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(b => b.BillNo.Contains(q) || (b.Customer != null && b.Customer.Name.Contains(q)));

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var asc = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "no" => asc ? query.OrderBy(b => b.BillNo) : query.OrderByDescending(b => b.BillNo),
            "total" => asc ? query.OrderBy(b => b.GrandTotal) : query.OrderByDescending(b => b.GrandTotal),
            "status" => asc ? query.OrderBy(b => b.Status) : query.OrderByDescending(b => b.Status),
            _ => asc
                ? query.OrderBy(b => b.BillDate).ThenBy(b => b.BillNo)
                : query.OrderByDescending(b => b.BillDate).ThenByDescending(b => b.BillNo)
        };

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewData["total"] = total;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);

        return View(rows);
    }

    // ────────────────────────────────────────────────────────
    // New bill → start POS session
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> NewBill()
    {
        await LoadLookupsAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewBill(Guid storeId, Guid warehouseId, Guid? customerId)
    {
        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var billId = await _pos.CreateBillAsync(storeId, warehouseId, customerId, userId);
        return RedirectToAction(nameof(Bill), new { id = billId });
    }

    // ────────────────────────────────────────────────────────
    // POS Billing screen (the main cashier view)
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Bill(Guid id)
    {
        var bill = await _db.PosBills
            .Include(b => b.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Unit)
            .Include(b => b.Payments)
            .Include(b => b.Store)
            .Include(b => b.Warehouse)
            .Include(b => b.Customer)
            .Include(b => b.CashierUser)
            .FirstOrDefaultAsync(b => b.PosBillId == id);

        if (bill is null) return NotFound();

        var companyIdStr = User.FindFirstValue("CompanyId");
        if (Guid.TryParse(companyIdStr, out var companyId))
        {
            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
            ViewBag.CompanyName = company?.Name ?? "RetailERP";
        }
        else
        {
            ViewBag.CompanyName = "RetailERP";
        }
        ViewBag.CashierName = bill.CashierUser?.UserName ?? User.Identity?.Name ?? "Cashier";

        return View(bill);
    }

    // ── AJAX endpoints for POS screen ──

    /// <summary>Barcode / SKU lookup (AJAX)</summary>
    [HttpGet]
    public async Task<IActionResult> LookupItem(string code, Guid warehouseId)
    {
        var result = await _pos.LookupItemAsync(code, warehouseId);
        if (result is null) return Json(new { success = false, message = "Item not found." });
        return Json(new { success = true, item = result });
    }

    /// <summary>Add line (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine([FromBody] AddLineReq req)
    {
        try
        {
            var line = await _pos.AddLineAsync(req.BillId, req.ItemId, req.Qty <= 0 ? 1 : req.Qty);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Update line qty (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateLineQty([FromBody] UpdateLineQtyReq req)
    {
        try
        {
            await _pos.UpdateLineQtyAsync(req.BillLineId, req.Qty);
            var line = await _db.PosBillLines.AsNoTracking().FirstAsync(l => l.PosBillLineId == req.BillLineId);
            var bill = await GetBillSummaryAsync(line.PosBillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Remove line (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine([FromBody] RemoveLineReq req)
    {
        try
        {
            var line = await _db.PosBillLines.AsNoTracking().FirstOrDefaultAsync(l => l.PosBillLineId == req.BillLineId);
            if (line is null) return Json(new { success = false, message = "Line not found." });

            var billId = line.PosBillId;
            await _pos.RemoveLineAsync(req.BillLineId);
            var bill = await GetBillSummaryAsync(billId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Add payment (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment([FromBody] AddPaymentReq req)
    {
        try
        {
            await _pos.AddPaymentAsync(req.BillId, req.Method, req.Amount, req.Reference);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Remove payment (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePayment([FromBody] RemovePaymentReq req)
    {
        try
        {
            await _pos.RemovePaymentAsync(req.PaymentId);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Complete bill and auto-create next bill with same store/warehouse (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteBill([FromBody] BillIdReq req)
    {
        try
        {
            // Get store/warehouse from current bill before completing
            var currentBill = await _db.PosBills.AsNoTracking()
                .FirstAsync(b => b.PosBillId == req.BillId);
            var storeId = currentBill.StoreId;
            var warehouseId = currentBill.WarehouseId;

            await _pos.CompleteBillAsync(req.BillId);

            // Auto-create next bill with same store/warehouse
            var userId = Guid.Parse(_userManager.GetUserId(User)!);
            var nextBillId = await _pos.CreateBillAsync(storeId, warehouseId, null, userId);

            return Json(new
            {
                success = true,
                receiptUrl = Url.Action(nameof(Receipt), new { id = req.BillId }),
                nextBillUrl = Url.Action(nameof(Bill), new { id = nextBillId })
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Cancel bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBill([FromBody] BillIdReq req)
    {
        try
        {
            await _pos.CancelBillAsync(req.BillId);
            return Json(new { success = true, redirectUrl = Url.Action(nameof(Index)) });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── Phase 6: Loyalty + Coupon AJAX endpoints ──

    /// <summary>Attach loyalty card to bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachLoyalty([FromBody] AttachLoyaltyReq req)
    {
        try
        {
            await _pos.AttachLoyaltyCardAsync(req.BillId, req.LoyaltyCardId);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Remove loyalty card from bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLoyalty([FromBody] BillIdReq req)
    {
        try
        {
            await _pos.RemoveLoyaltyCardAsync(req.BillId);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Redeem loyalty points on bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RedeemLoyalty([FromBody] RedeemLoyaltyReq req)
    {
        try
        {
            await _pos.RedeemLoyaltyOnBillAsync(req.BillId, req.Points);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Apply coupon to bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCoupon([FromBody] ApplyCouponReq req)
    {
        try
        {
            await _pos.ApplyCouponAsync(req.BillId, req.CouponCode);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Remove coupon from bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCoupon([FromBody] BillIdReq req)
    {
        try
        {
            await _pos.RemoveCouponAsync(req.BillId);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── Sprint 7: Hold / Unhold ──

    /// <summary>Hold current bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HoldBill([FromBody] BillIdReq req)
    {
        try
        {
            await _pos.HoldBillAsync(req.BillId);

            // Auto-create next bill with same store/warehouse
            var currentBill = await _db.PosBills.AsNoTracking()
                .FirstAsync(b => b.PosBillId == req.BillId);
            var userId = Guid.Parse(_userManager.GetUserId(User)!);
            var nextBillId = await _pos.CreateBillAsync(currentBill.StoreId, currentBill.WarehouseId, null, userId);

            return Json(new { success = true, nextBillUrl = Url.Action(nameof(Bill), new { id = nextBillId }) });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    /// <summary>Get list of held bills (AJAX)</summary>
    [HttpGet]
    public async Task<IActionResult> GetHeldBills()
    {
        var held = await _pos.GetHeldBillsAsync();
        return Json(new { success = true, bills = held });
    }

    /// <summary>Resume a held bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnholdBill([FromBody] BillIdReq req)
    {
        try
        {
            await _pos.UnholdBillAsync(req.BillId);
            return Json(new { success = true, billUrl = Url.Action(nameof(Bill), new { id = req.BillId }) });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    // ── Sprint 7: Line-level discount ──

    /// <summary>Set line discount % (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLineDiscount([FromBody] SetLineDiscountReq req)
    {
        try
        {
            await _pos.SetLineDiscountAsync(req.BillLineId, req.DiscountPercent);
            var line = await _db.PosBillLines.AsNoTracking().FirstAsync(l => l.PosBillLineId == req.BillLineId);
            var bill = await GetBillSummaryAsync(line.PosBillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    // ── Sprint 7: Bill-level discount / charge ──

    /// <summary>Set additional discount % on bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAddDiscount([FromBody] SetBillDiscountReq req)
    {
        try
        {
            await _pos.SetAddDiscountAsync(req.BillId, req.Percent);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    /// <summary>Set additional charge % on bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAddCharge([FromBody] SetBillDiscountReq req)
    {
        try
        {
            await _pos.SetAddChargeAsync(req.BillId, req.Percent);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    // ── Sprint 7: Auto-apply promotions ──

    /// <summary>Apply all active promotions to bill (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyPromotions([FromBody] BillIdReq req)
    {
        try
        {
            var applied = await _promo.ApplyPromotionsAsync(req.BillId);
            var bill = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill, applied });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    // ────────────────────────────────────────────────────────
    // Receipt (print-friendly)
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item)
            .Include(b => b.Payments)
            .Include(b => b.Store)
            .Include(b => b.Warehouse)
            .Include(b => b.Customer)
            .Include(b => b.CashierUser)
            .FirstOrDefaultAsync(b => b.PosBillId == id);

        if (bill is null) return NotFound();
        return View(bill);
    }

    // ────────────────────────────────────────────────────────
    // Returns
    // ────────────────────────────────────────────────────────

    /// <summary>Returns list</summary>
    [HttpGet]
    public async Task<IActionResult> Returns(string? q, byte? status, string sort = "date", string dir = "desc", int page = 1, int pageSize = 20)
    {
        q = (q ?? "").Trim();
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        ViewData["q"] = q;
        ViewData["status"] = status;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.PosReturns
            .AsNoTracking()
            .Include(r => r.OriginalBill)
            .Include(r => r.Customer)
            .Include(r => r.Store)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r => r.ReturnNo.Contains(q) || (r.OriginalBill != null && r.OriginalBill.BillNo.Contains(q)));

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var asc = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "no" => asc ? query.OrderBy(r => r.ReturnNo) : query.OrderByDescending(r => r.ReturnNo),
            "refund" => asc ? query.OrderBy(r => r.TotalRefund) : query.OrderByDescending(r => r.TotalRefund),
            _ => asc ? query.OrderBy(r => r.ReturnDate) : query.OrderByDescending(r => r.ReturnDate)
        };

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewData["total"] = total;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);

        return View(rows);
    }

    /// <summary>Start a return process from a completed bill</summary>
    [HttpGet]
    public async Task<IActionResult> NewReturn(Guid billId)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item)
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.PosBillId == billId);

        if (bill is null) return NotFound();
        if (bill.Status != 2)
        {
            TempData["Err"] = "Can only return items from completed bills.";
            return RedirectToAction(nameof(Index));
        }

        return View(bill);
    }

    /// <summary>Process return (POST)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessReturn(Guid billId, string? reason, string refundMethod, List<ReturnLineVm> lines)
    {
        try
        {
            var returnLines = lines
                .Where(l => l.Qty > 0)
                .Select(l => new PosBillingService.ReturnLineInput
                {
                    OriginalBillLineId = l.OriginalBillLineId,
                    Qty = l.Qty
                })
                .ToList();

            if (returnLines.Count == 0)
            {
                TempData["Err"] = "Select at least one item to return.";
                return RedirectToAction(nameof(NewReturn), new { billId });
            }

            var userId = Guid.Parse(_userManager.GetUserId(User)!);
            var returnId = await _pos.ProcessReturnAsync(billId, returnLines, reason, refundMethod, userId);

            TempData["Ok"] = "Return processed successfully.";
            return RedirectToAction(nameof(ReturnDetails), new { id = returnId });
        }
        catch (Exception ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(NewReturn), new { billId });
        }
    }

    /// <summary>Return details view</summary>
    [HttpGet]
    public async Task<IActionResult> ReturnDetails(Guid id)
    {
        var ret = await _db.PosReturns
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.OriginalBill)
            .Include(r => r.Store)
            .Include(r => r.Customer)
            .Include(r => r.ProcessedByUser)
            .FirstOrDefaultAsync(r => r.PosReturnId == id);

        if (ret is null) return NotFound();
        return View(ret);
    }

    // ── Helpers ──

    private async Task LoadLookupsAsync()
    {
        ViewBag.Stores = new SelectList(
            await _db.Stores.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(),
            "StoreId", "Name");

        ViewBag.Warehouses = new SelectList(
            await _db.Warehouses.AsNoTracking().OrderBy(w => w.Name).ToListAsync(),
            "WarehouseId", "Name");

        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "CustomerId", "Name");
    }

    private async Task<object> GetBillSummaryAsync(Guid billId)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item).ThenInclude(i => i.Unit)
            .Include(b => b.Payments)
            .Include(b => b.LoyaltyCard)
            .Include(b => b.Coupon)
            .FirstAsync(b => b.PosBillId == billId);

        return new
        {
            bill.PosBillId,
            bill.BillNo,
            bill.SubTotal,
            bill.TaxTotal,
            bill.DiscountTotal,
            bill.GrandTotal,
            bill.Status,

            // Sprint 7: Bill-level discount/charge/roundoff
            bill.AddDiscountPercent,
            bill.AddDiscountAmount,
            bill.AddChargePercent,
            bill.AddChargeAmount,
            bill.RoundOff,

            // Loyalty
            bill.LoyaltyCardId,
            bill.LoyaltyPointsRedeemed,
            bill.LoyaltyDiscount,
            LoyaltyCardNumber = bill.LoyaltyCard?.CardNumber,
            LoyaltyPointsBalance = bill.LoyaltyCard?.PointsBalance,
            LoyaltyTier = bill.LoyaltyCard != null
                ? Services.LoyaltyService.GetTierName(bill.LoyaltyCard.Tier) : null,

            // Coupon
            bill.CouponId,
            bill.CouponDiscount,
            CouponCode = bill.Coupon?.Code,

            Lines = bill.Lines.Select(l => new
            {
                l.PosBillLineId,
                l.ItemId,
                l.SkuSnapshot,
                l.BarcodeSnapshot,
                l.ItemNameSnapshot,
                l.Qty,
                l.UnitPrice,
                l.GstPercentSnapshot,
                l.DiscountAmount,
                l.DiscountPercent,
                l.NetRate,
                l.AppliedPromotionId,
                l.LineTotal,
                ItemName = l.Item?.Name ?? l.ItemNameSnapshot,
                Mrp = l.Item?.MRP ?? l.UnitPrice,
                UnitName = l.Item?.Unit?.Name ?? "—"
            }),
            Payments = bill.Payments.Where(p => !p.IsRefund).Select(p => new
            {
                p.PaymentId,
                p.Method,
                p.Amount,
                p.Reference
            }),
            TotalPaid = bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount),
            Remaining = bill.GrandTotal - bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount)
        };
    }

    // ── View Models / Request DTOs ──
    public class ReturnLineVm
    {
        public Guid OriginalBillLineId { get; set; }
        public decimal Qty { get; set; }
    }

    public class AddLineReq
    {
        public Guid BillId { get; set; }
        public Guid ItemId { get; set; }
        public decimal Qty { get; set; } = 1;
    }

    public class UpdateLineQtyReq
    {
        public Guid BillLineId { get; set; }
        public decimal Qty { get; set; }
    }

    public class RemoveLineReq
    {
        public Guid BillLineId { get; set; }
    }

    public class AddPaymentReq
    {
        public Guid BillId { get; set; }
        public string Method { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

    public class RemovePaymentReq
    {
        public Guid PaymentId { get; set; }
        public Guid BillId { get; set; }
    }

    public class BillIdReq
    {
        public Guid BillId { get; set; }
    }

    public class AttachLoyaltyReq
    {
        public Guid BillId { get; set; }
        public Guid LoyaltyCardId { get; set; }
    }

    public class RedeemLoyaltyReq
    {
        public Guid BillId { get; set; }
        public decimal Points { get; set; }
    }

    public class ApplyCouponReq
    {
        public Guid BillId { get; set; }
        public string CouponCode { get; set; } = "";
    }

    // Sprint 7 DTOs
    public class SetLineDiscountReq
    {
        public Guid BillLineId { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    public class SetBillDiscountReq
    {
        public Guid BillId { get; set; }
        public decimal Percent { get; set; }
    }

    // ────────────────────────────────────────────────────────
    // Sprint 6: Receipt PDF download
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ReceiptPdf(Guid id)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item)
            .Include(b => b.Payments)
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .Include(b => b.CashierUser)
            .FirstOrDefaultAsync(b => b.PosBillId == id);

        if (bill is null) return NotFound();

        var companyId = Guid.Parse(User.FindFirstValue("CompanyId") ?? Guid.Empty.ToString());
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
        if (company is null) return NotFound();

        // Find default POS receipt template for this company
        var template = await _db.BillTemplates
            .AsNoTracking()
            .Where(t => t.TemplateType == 1 && t.IsDefault)
            .FirstOrDefaultAsync();

        if (template is null)
        {
            return BadRequest("No default receipt template found. Please create one in Bill Templates and mark it as default.");
        }

        var pdf = _receiptPdf.Generate(bill, template, company);
        return File(pdf, "application/pdf", $"Receipt_{bill.BillNo}.pdf");
    }
}

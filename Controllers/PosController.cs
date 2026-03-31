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
using System.Net.Mail;

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
    public async Task<IActionResult> Index(string? q, byte? status, DateTime? dateFrom, DateTime? dateTo, string sort = "date", string dir = "desc", int page = 1, int pageSize = 20)
    {
        q = (q ?? "").Trim();
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        ViewData["q"] = q;
        ViewData["status"] = status;
        ViewData["dateFrom"] = dateFrom.HasValue ? dateFrom.Value.ToString("yyyy-MM-dd") : "";
        ViewData["dateTo"] = dateTo.HasValue ? dateTo.Value.ToString("yyyy-MM-dd") : "";
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.PosBills
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(b => b.BillNo.Contains(q) || (b.Customer != null && b.Customer.Name.Contains(q)));

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        if (dateFrom.HasValue)
        {
            var d0 = dateFrom.Value.Date;
            query = query.Where(b => b.BillDate >= d0);
        }

        if (dateTo.HasValue)
        {
            var d1 = dateTo.Value.Date.AddDays(1);
            query = query.Where(b => b.BillDate < d1);
        }

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
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new PosBill
            {
                PosBillId = b.PosBillId,
                BillNo = b.BillNo,
                BillDate = b.BillDate,
                GrandTotal = b.GrandTotal,
                Status = b.Status,
                Store = b.Store == null ? null : new Store
                {
                    Name = b.Store.Name
                },
                Customer = b.Customer == null ? null : new Customer
                {
                    Name = b.Customer.Name
                },
                CashierUser = b.CashierUser == null ? null : new ApplicationUser
                {
                    DisplayName = b.CashierUser.DisplayName,
                    Email = b.CashierUser.Email
                }
            })
            .ToListAsync();

        ViewData["total"] = total;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);

        return View(rows);
    }

    // ────────────────────────────────────────────────────────
    // New bill → start POS session
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// One-click start when the user has saved default store + warehouse; otherwise redirects to <see cref="NewBill"/>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartQuick()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.DefaultPosStoreId is null || user.DefaultPosWarehouseId is null)
        {
            TempData["Info"] = "Pick store and warehouse once, then tick “Save as my default for POS” to skip this step next time.";
            return RedirectToAction(nameof(NewBill));
        }

        if (!await IsValidPosStoreAndWarehouseAsync(user.DefaultPosStoreId.Value, user.DefaultPosWarehouseId.Value))
        {
            TempData["Err"] = "Your saved default store or warehouse is no longer available. Please choose again.";
            user.DefaultPosStoreId = null;
            user.DefaultPosWarehouseId = null;
            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(NewBill));
        }

        var userId = Guid.Parse(_userManager.GetUserId(User)!);
        var billId = await _pos.CreateBillAsync(user.DefaultPosStoreId.Value, user.DefaultPosWarehouseId.Value, null, userId);
        return RedirectToAction(nameof(Bill), new { id = billId });
    }

    [HttpGet]
    public async Task<IActionResult> NewBill()
    {
        var user = await _userManager.GetUserAsync(User);
        Guid? selStore = null;
        Guid? selWarehouse = null;
        var hasValidDefaults = false;

        if (user?.DefaultPosStoreId is { } ds && user.DefaultPosWarehouseId is { } dw
            && await IsValidPosStoreAndWarehouseAsync(ds, dw))
        {
            hasValidDefaults = true;
            selStore = ds;
            selWarehouse = dw;
        }

        ViewBag.HasValidDefaults = hasValidDefaults;
        ViewBag.SelectedStoreId = selStore;
        ViewBag.SelectedWarehouseId = selWarehouse;

        await LoadLookupsAsync(selStore, selWarehouse);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewBill(Guid storeId, Guid warehouseId, Guid? customerId, bool saveAsDefault = false)
    {
        if (!await IsValidPosStoreAndWarehouseAsync(storeId, warehouseId))
        {
            TempData["Err"] = "That store and warehouse combination is not valid. Warehouses must belong to the selected store (or be company-wide).";
            ViewBag.HasValidDefaults = false;
            ViewBag.SelectedStoreId = storeId;
            ViewBag.SelectedWarehouseId = warehouseId;
            await LoadLookupsAsync(storeId, warehouseId);
            return View();
        }

        if (saveAsDefault)
        {
            var appUser = await _userManager.GetUserAsync(User);
            if (appUser is not null)
            {
                appUser.DefaultPosStoreId = storeId;
                appUser.DefaultPosWarehouseId = warehouseId;
                await _userManager.UpdateAsync(appUser);
            }
        }

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
            .Include(b => b.Lines).ThenInclude(l => l.Item!).ThenInclude(i => i.Unit)
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

    /// <summary>Update POS bill customer by selecting/creating from inline details (AJAX).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCustomer([FromBody] SetCustomerReq req)
    {
        try
        {
            var bill = await _db.PosBills.FirstOrDefaultAsync(b => b.PosBillId == req.BillId);
            if (bill is null) return Json(new { success = false, message = "Bill not found." });
            if (bill.Status != 1) return Json(new { success = false, message = "Only open bills can be edited." });

            if (req.CustomerId.HasValue)
            {
                var exists = await _db.Customers.AsNoTracking().AnyAsync(c => c.CustomerId == req.CustomerId.Value);
                if (!exists) return Json(new { success = false, message = "Customer not found." });
                bill.CustomerId = req.CustomerId.Value;
                await _db.SaveChangesAsync();
                var summaryLinked = await GetBillSummaryAsync(req.BillId);
                return Json(new { success = true, bill = summaryLinked });
            }

            var name = (req.Name ?? "").Trim();
            var phoneRaw = (req.Phone ?? "").Trim();
            var digits = new string(phoneRaw.Where(char.IsDigit).ToArray());
            var phone = digits.Length == 10 ? digits : "";
            var email = (req.Email ?? "").Trim();

            // Empty values = walk-in customer
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            {
                bill.CustomerId = null;
                await _db.SaveChangesAsync();
                var summaryWalkIn = await GetBillSummaryAsync(req.BillId);
                return Json(new { success = true, bill = summaryWalkIn });
            }

            if (!string.IsNullOrWhiteSpace(phone) && (phone[0] < '6' || phone[0] > '9'))
                return Json(new { success = false, message = "Phone must be a valid 10-digit Indian mobile." });
            if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
                return Json(new { success = false, message = "Email format is invalid." });

            Customer? customer = null;
            if (!string.IsNullOrWhiteSpace(phone))
                customer = await _db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);

            if (customer is null && !string.IsNullOrWhiteSpace(name))
                customer = await _db.Customers.FirstOrDefaultAsync(c => c.Name == name);

            if (customer is null)
            {
                customer = new Customer
                {
                    CustomerId = Guid.NewGuid(),
                    Name = string.IsNullOrWhiteSpace(name) ? $"Customer {phone}" : name,
                    Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email
                };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(name) && !string.Equals(customer.Name, name, StringComparison.Ordinal))
                    customer.Name = name;
                if (!string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(customer.Phone))
                    customer.Phone = phone;
                if (!string.IsNullOrWhiteSpace(email))
                    customer.Email = email;
                await _db.SaveChangesAsync();
            }

            bill.CustomerId = customer.CustomerId;
            await _db.SaveChangesAsync();

            var summary = await GetBillSummaryAsync(req.BillId);
            return Json(new { success = true, bill = summary });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>Quick Add Open Item during Billing (AJAX)</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddItem([FromBody] QuickAddItemReq req)
    {
        using var tran = await _db.Database.BeginTransactionAsync();
        try
        {
            var sku = $"QA-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Random.Shared.Next(100, 999)}";
            var item = new Item
            {
                Name = req.Name?.Trim() ?? "New Item",
                UnitPrice = req.Price,
                MRP = req.Price,
                Barcode = string.IsNullOrWhiteSpace(req.Barcode) ? null : req.Barcode.Trim(),
                SKU = sku,
                IsActive = true
            };

            _db.Items.Add(item);
            await _db.SaveChangesAsync();

            // Add stock so it can be billed immediately
            var stockTx = new StockTransaction
            {
                ItemId = item.ItemId,
                WarehouseId = req.WarehouseId,
                Qty = 100, // Enough to sell
                Type = "ADJUSTMENT",
                OccurredAtUtc = DateTime.UtcNow
            };
            _db.StockTransactions.Add(stockTx);

            var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ItemId == item.ItemId && s.WarehouseId == req.WarehouseId);
            if (stock == null)
            {
                stock = new Stock { ItemId = item.ItemId, WarehouseId = req.WarehouseId, Quantity = 100 };
                _db.Stocks.Add(stock);
            }
            else
            {
                stock.Quantity += 100;
            }

            await _db.SaveChangesAsync();
            await tran.CommitAsync();

            return Json(new { success = true, itemId = item.ItemId });
        }
        catch (Exception ex)
        {
            await tran.RollbackAsync();
            return Json(new { success = false, message = "Could not create item: " + ex.Message });
        }
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
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new PosReturn
            {
                PosReturnId = r.PosReturnId,
                ReturnNo = r.ReturnNo,
                ReturnDate = r.ReturnDate,
                OriginalBillId = r.OriginalBillId,
                TotalRefund = r.TotalRefund,
                Status = r.Status,
                OriginalBill = r.OriginalBill == null ? null : new PosBill
                {
                    BillNo = r.OriginalBill.BillNo
                },
                Store = r.Store == null ? null : new Store
                {
                    Name = r.Store.Name
                },
                Customer = r.Customer == null ? null : new Customer
                {
                    Name = r.Customer.Name
                }
            })
            .ToListAsync();

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

    private async Task LoadLookupsAsync(Guid? selectedStoreId = null, Guid? selectedWarehouseId = null)
    {
        var stores = await _db.Stores.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        var warehouses = await _db.Warehouses.AsNoTracking().OrderBy(w => w.Name).ToListAsync();

        ViewBag.Stores = new SelectList(stores, "StoreId", "Name", selectedStoreId);
        ViewBag.WarehouseRows = warehouses;
        ViewBag.SelectedWarehouseId = selectedWarehouseId;

        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "CustomerId", "Name");
    }

    /// <summary>
    /// Store must be active; warehouse must match store when <see cref="Warehouse.StoreId"/> is set; optional tenant company match.
    /// </summary>
    private async Task<bool> IsValidPosStoreAndWarehouseAsync(Guid storeId, Guid warehouseId)
    {
        var hasTenant = TryGetScopedCompanyId(out var tenantCompanyId);

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.StoreId == storeId);
        if (store is null || !store.IsActive) return false;

        if (hasTenant && store.CompanyId.HasValue && store.CompanyId.Value != tenantCompanyId)
            return false;

        var wh = await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.WarehouseId == warehouseId);
        if (wh is null) return false;

        if (hasTenant && wh.CompanyId.HasValue && wh.CompanyId.Value != tenantCompanyId)
            return false;

        if (wh.StoreId.HasValue && wh.StoreId.Value != storeId)
            return false;

        return true;
    }

    private bool TryGetScopedCompanyId(out Guid companyId)
    {
        companyId = default;
        var s = User.FindFirstValue("CompanyId");
        return !string.IsNullOrEmpty(s) && Guid.TryParse(s, out companyId) && companyId != Guid.Empty;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<object> GetBillSummaryAsync(Guid billId)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item!).ThenInclude(i => i.Unit)
            .Include(b => b.Payments)
            .Include(b => b.LoyaltyCard)
            .Include(b => b.Coupon)
            .Include(b => b.Customer)
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
            bill.CustomerId,
            CustomerName = bill.Customer?.Name,
            CustomerPhone = bill.Customer?.Phone,
            CustomerEmail = bill.Customer?.Email,

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

    public class SetCustomerReq
    {
        public Guid BillId { get; set; }
        public Guid? CustomerId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
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

    public class QuickAddItemReq
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string? Barcode { get; set; }
        public Guid WarehouseId { get; set; }
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

    // ────────────────────────────────────────────────────────
    // Sprint 10: PWA Offline — cache all items for offline lookup
    // ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> AllItems()
    {
        var items = await _db.Items.AsNoTracking()
            .Include(i => i.Unit)
            .Include(i => i.Category)
            .Where(i => i.IsActive)
            .Select(i => new
            {
                itemId = i.ItemId,
                sku = i.SKU,
                barcode = i.Barcode,
                name = i.Name,
                unitPrice = i.UnitPrice,
                mrp = i.MRP,
                gstPercent = i.GstPercent,
                hsnCode = i.HsnCode,
                unitName = i.Unit != null ? i.Unit.Name : null,
                categoryName = i.Category != null ? i.Category.Name : null
            })
            .ToListAsync();

        return Json(items);
    }
}

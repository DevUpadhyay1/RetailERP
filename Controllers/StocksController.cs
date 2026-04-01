using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models;
using RetailERP.Services;

namespace RetailERP.Controllers
{
    [Authorize(Roles = "Admin,Manager,Inventory")]
    public class StocksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuditService _audit;


        public StocksController(ApplicationDbContext context, AuditService audit)
        {
            _context = context;
            _audit = audit;

        }

        // GET: Stocks
        public async Task<IActionResult> Index(string? q, string sort = "warehouse", string dir = "asc", int page = 1, int pageSize = 20)
        {
            q = (q ?? "").Trim();
            if (page < 1) page = 1;
            if (pageSize is < 10 or > 200) pageSize = 20;

            ViewData["q"] = q;
            ViewData["sort"] = sort;
            ViewData["dir"] = dir;
            ViewData["page"] = page;
            ViewData["pageSize"] = pageSize;

            var query = _context.Stocks
                .AsNoTracking()
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    (x.Item != null && (x.Item.SKU.Contains(q) || x.Item.Name.Contains(q))) ||
                    (x.Warehouse != null && x.Warehouse.Name.Contains(q))
                );
            }

            var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sort?.ToLowerInvariant() switch
            {
                "sku" => ascending
                    ? query.OrderBy(x => x.Item!.SKU).ThenBy(x => x.Warehouse!.Name)
                    : query.OrderByDescending(x => x.Item!.SKU).ThenByDescending(x => x.Warehouse!.Name),
                "item" => ascending
                    ? query.OrderBy(x => x.Item!.Name).ThenBy(x => x.Item!.SKU)
                    : query.OrderByDescending(x => x.Item!.Name).ThenByDescending(x => x.Item!.SKU),
                "qty" => ascending
                    ? query.OrderBy(x => x.Quantity).ThenBy(x => x.Item!.SKU)
                    : query.OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Item!.SKU),
                _ => ascending
                    ? query.OrderBy(x => x.Warehouse!.Name).ThenBy(x => x.Item!.SKU)
                    : query.OrderByDescending(x => x.Warehouse!.Name).ThenByDescending(x => x.Item!.SKU)
            };

            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewData["total"] = total;
            ViewData["totalPages"] = totalPages < 1 ? 1 : totalPages;
            ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
            ViewData["to"] = Math.Min(page * pageSize, total);

            return View(data);
        }

        // GET: Stocks/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var stock = await _context.Stocks
                .AsNoTracking()
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .FirstOrDefaultAsync(m => m.StockId == id);

            if (stock == null) return NotFound();

            return View(stock);
        }

        // GET: Stocks/Create
        public IActionResult Create()
        {
            ViewData["ItemId"] = new SelectList(_context.Items.OrderBy(x => x.SKU), "ItemId", "SKU");
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses.OrderBy(x => x.Name), "WarehouseId", "Name");
            return View();
        }

        // POST: Stocks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StockId,ItemId,WarehouseId,Quantity")] Stock stock)
        {
            if (ModelState.IsValid)
            {
                var exists = await _context.Stocks.AnyAsync(x =>
                    x.ItemId == stock.ItemId && x.WarehouseId == stock.WarehouseId);

                if (exists)
                {
                    ModelState.AddModelError("", "Stock row already exists for this Item and Warehouse. Use Adjust on existing row.");
                }
                else
                {
                    var openingQty = stock.Quantity;
                    stock.StockId = Guid.NewGuid();
                    stock.Quantity = 0;

                    try
                    {
                        using var tx = await _context.Database.BeginTransactionAsync();

                        _context.Stocks.Add(stock);
                        await _context.SaveChangesAsync();

                        if (openingQty != 0)
                        {
                            var storeId = await _context.Warehouses
                                .AsNoTracking()
                                .Where(w => w.WarehouseId == stock.WarehouseId)
                                .Select(w => (Guid?)w.StoreId)
                                .FirstOrDefaultAsync();

                            var companyId = await _context.Items
                                .AsNoTracking()
                                .Where(i => i.ItemId == stock.ItemId)
                                .Select(i => i.CompanyId)
                                .FirstOrDefaultAsync();

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                StockTransactionId = Guid.NewGuid(),
                                OccurredAtUtc = DateTime.UtcNow,
                                Type = "ADJUSTMENT",
                                ItemId = stock.ItemId,
                                WarehouseId = stock.WarehouseId,
                                StoreId = storeId,
                                Qty = openingQty,
                                RefType = "StockCreate",
                                RefId = stock.StockId.ToString(),
                                Reason = "Opening stock during stock-row creation",
                                CompanyId = companyId
                            });

                            stock.Quantity = openingQty;
                            await _context.SaveChangesAsync();
                        }

                        await tx.CommitAsync();
                        TempData["Ok"] = openingQty == 0
                            ? "Stock row created with zero quantity."
                            : "Stock row created and opening stock posted to ledger.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateException)
                    {
                        ModelState.AddModelError("", "Unable to save stock. Check uniqueness and try again.");
                    }
                }
            }

            ViewData["ItemId"] = new SelectList(_context.Items.OrderBy(x => x.SKU), "ItemId", "SKU", stock.ItemId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses.OrderBy(x => x.Name), "WarehouseId", "Name", stock.WarehouseId);
            return View(stock);
        }

        // GET: Stocks/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var exists = await _context.Stocks.AsNoTracking().AnyAsync(x => x.StockId == id.Value);
            if (!exists) return NotFound();

            TempData["Err"] = "Direct stock edit is disabled. Use Adjust to keep ledger audit intact.";
            return RedirectToAction(nameof(Adjust), new { id = id.Value, returnUrl = Url.Action(nameof(Index)) });
        }

        // POST: Stocks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Guid id, [Bind("StockId,ItemId,WarehouseId,Quantity")] Stock stock)
        {
            if (id != stock.StockId) return NotFound();
            TempData["Err"] = "Direct stock edit is disabled. Use Adjust to keep ledger audit intact.";
            return RedirectToAction(nameof(Adjust), new { id = stock.StockId, returnUrl = Url.Action(nameof(Index)) });
        }

        // GET: Stocks/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var stock = await _context.Stocks
                .AsNoTracking()
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .FirstOrDefaultAsync(m => m.StockId == id);

            if (stock == null) return NotFound();

            return View(stock);
        }

        // POST: Stocks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var stock = await _context.Stocks.FindAsync(id);
            if (stock != null)
            {
                _context.Stocks.Remove(stock);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Stocks/Adjust/5
        [HttpGet]
        public async Task<IActionResult> Adjust(Guid? id, string? returnUrl = null)
        {
            if (id == null) return NotFound();

            var stock = await _context.Stocks
                .AsNoTracking()
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .FirstOrDefaultAsync(s => s.StockId == id);

            if (stock == null) return NotFound();

            var vm = new StockAdjustVm
            {
                StockId = stock.StockId,
                ItemLabel = stock.Item == null ? "" : $"{stock.Item.SKU} - {stock.Item.Name}",
                WarehouseName = stock.Warehouse?.Name ?? "",
                CurrentQty = stock.Quantity,
                DeltaQty = 0,
                ReturnUrl = returnUrl
            };

            ViewData["CanApplyDirect"] = User.IsInRole("Admin");

            return View(vm);
        }

        // POST: Stocks/Adjust
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(StockAdjustVm vm)
        {
            var canApplyDirect = User.IsInRole("Admin");

            if (!ModelState.IsValid)
            {
                ViewData["CanApplyDirect"] = canApplyDirect;
                return View(vm);
            }

            if (vm.DeltaQty == 0)
            {
                ModelState.AddModelError(nameof(vm.DeltaQty), "Delta must not be zero.");
                ViewData["CanApplyDirect"] = canApplyDirect;
                return View(vm);
            }

            if (!canApplyDirect)
            {
                var stockForRequest = await _context.Stocks
                    .Include(s => s.Item)
                    .Include(s => s.Warehouse)
                    .FirstOrDefaultAsync(s => s.StockId == vm.StockId);

                if (stockForRequest == null) return NotFound();

                var request = new StockAdjustmentRequest
                {
                    StockAdjustmentRequestId = Guid.NewGuid(),
                    StockId = stockForRequest.StockId,
                    DeltaQty = vm.DeltaQty,
                    Reason = vm.Reason,
                    RequestedByUserId = GetCurrentUserId(),
                    RequestedAtUtc = DateTime.UtcNow,
                    Status = 1,
                    CompanyId = stockForRequest.Item?.CompanyId
                };

                _context.StockAdjustmentRequests.Add(request);
                await _context.SaveChangesAsync();

                try
                {
                    await _audit.LogAsync(
                        action: "StockAdjustRequested",
                        entityType: "StockAdjustmentRequest",
                        entityId: request.StockAdjustmentRequestId.ToString(),
                        data: new
                        {
                            request.StockId,
                            request.DeltaQty,
                            request.Reason,
                            request.RequestedByUserId
                        }
                    );
                }
                catch { }

                TempData["Ok"] = "Adjustment request submitted for admin approval.";

                if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                    return Redirect(vm.ReturnUrl);

                return RedirectToAction(nameof(AdjustmentRequests));
            }

            using var tx = await _context.Database.BeginTransactionAsync();

            var stock = await _context.Stocks
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .FirstOrDefaultAsync(s => s.StockId == vm.StockId);

            if (stock == null) return NotFound();

            var newQty = stock.Quantity + vm.DeltaQty;
            if (newQty < 0)
            {
                vm.CurrentQty = stock.Quantity;
                vm.ItemLabel = stock.Item == null ? "" : $"{stock.Item.SKU} - {stock.Item.Name}";
                vm.WarehouseName = stock.Warehouse?.Name ?? "";
                ModelState.AddModelError(nameof(vm.DeltaQty), "Resulting stock cannot be negative.");
                ViewData["CanApplyDirect"] = canApplyDirect;
                return View(vm);
            }

            _context.StockTransactions.Add(new StockTransaction
            {
                StockTransactionId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Type = "ADJUSTMENT",
                ItemId = stock.ItemId,
                WarehouseId = stock.WarehouseId,
                StoreId = stock.Warehouse?.StoreId,
                Qty = vm.DeltaQty,
                RefType = "StockAdjust",
                RefId = stock.StockId.ToString(),
                Reason = vm.Reason,
                ActorUserId = GetCurrentUserId(),
                CompanyId = stock.Item?.CompanyId
            });

            stock.Quantity = newQty;
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // Audit: ONE record for manual adjustment
            try
            {
                await _audit.LogAsync(
                    action: "StockAdjusted",
                    entityType: "Stock",
                    entityId: stock.StockId.ToString(),
                    data: new
                    {
                        stock.StockId,
                        stock.ItemId,
                        stock.WarehouseId,
                        Delta = vm.DeltaQty,
                        NewQty = newQty,
                        vm.Reason
                    }
                );
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> AdjustmentRequests(byte? status = null)
        {
            var requesterId = GetCurrentUserId();
            var canApprove = User.IsInRole("Admin");

            var query = _context.StockAdjustmentRequests
                .AsNoTracking()
                .Include(r => r.Stock)
                    .ThenInclude(s => s!.Item)
                .Include(r => r.Stock)
                    .ThenInclude(s => s!.Warehouse)
                .Include(r => r.RequestedByUser)
                .Include(r => r.ReviewedByUser)
                .AsQueryable();

            if (!canApprove && requesterId.HasValue)
            {
                query = query.Where(r => r.RequestedByUserId == requesterId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            var rows = await query
                .OrderBy(r => r.Status == 1 ? 0 : 1)
                .ThenByDescending(r => r.RequestedAtUtc)
                .Take(250)
                .ToListAsync();

            ViewData["status"] = status;
            ViewData["canApprove"] = canApprove;
            return View(rows);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(Guid id, string? reviewNote = null, string? returnUrl = null)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            var req = await _context.StockAdjustmentRequests
                .Include(r => r.Stock)
                    .ThenInclude(s => s!.Item)
                .Include(r => r.Stock)
                    .ThenInclude(s => s!.Warehouse)
                .FirstOrDefaultAsync(r => r.StockAdjustmentRequestId == id && r.Status == 1);

            if (req?.Stock == null)
            {
                TempData["Err"] = "Adjustment request not found or already processed.";
                return RedirectToLocal(returnUrl, nameof(AdjustmentRequests));
            }

            var newQty = req.Stock.Quantity + req.DeltaQty;
            if (newQty < 0)
            {
                req.Status = 3;
                req.ReviewedByUserId = GetCurrentUserId();
                req.ReviewedAtUtc = DateTime.UtcNow;
                req.ReviewNote = "Auto-rejected: resulting stock would become negative.";
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Err"] = "Request auto-rejected because resulting stock would be negative.";
                return RedirectToLocal(returnUrl, nameof(AdjustmentRequests));
            }

            var stockTxn = new StockTransaction
            {
                StockTransactionId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Type = "ADJUSTMENT",
                ItemId = req.Stock.ItemId,
                WarehouseId = req.Stock.WarehouseId,
                StoreId = req.Stock.Warehouse?.StoreId,
                Qty = req.DeltaQty,
                RefType = "StockAdjustRequest",
                RefId = req.StockAdjustmentRequestId.ToString(),
                Reason = req.Reason,
                ActorUserId = GetCurrentUserId(),
                CompanyId = req.CompanyId ?? req.Stock.Item?.CompanyId
            };

            _context.StockTransactions.Add(stockTxn);
            req.Stock.Quantity = newQty;

            req.Status = 2;
            req.ReviewedByUserId = GetCurrentUserId();
            req.ReviewedAtUtc = DateTime.UtcNow;
            req.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim();
            req.AppliedStockTransactionId = stockTxn.StockTransactionId;

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            try
            {
                await _audit.LogAsync(
                    action: "StockAdjustApproved",
                    entityType: "StockAdjustmentRequest",
                    entityId: req.StockAdjustmentRequestId.ToString(),
                    data: new
                    {
                        req.StockId,
                        req.DeltaQty,
                        req.Reason,
                        NewQty = newQty,
                        req.ReviewedByUserId
                    }
                );
            }
            catch { }

            TempData["Ok"] = "Adjustment request approved and applied.";
            return RedirectToLocal(returnUrl, nameof(AdjustmentRequests));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(Guid id, string? reviewNote = null, string? returnUrl = null)
        {
            var req = await _context.StockAdjustmentRequests
                .FirstOrDefaultAsync(r => r.StockAdjustmentRequestId == id && r.Status == 1);

            if (req == null)
            {
                TempData["Err"] = "Adjustment request not found or already processed.";
                return RedirectToLocal(returnUrl, nameof(AdjustmentRequests));
            }

            req.Status = 3;
            req.ReviewedByUserId = GetCurrentUserId();
            req.ReviewedAtUtc = DateTime.UtcNow;
            req.ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? "Rejected by admin" : reviewNote.Trim();
            await _context.SaveChangesAsync();

            try
            {
                await _audit.LogAsync(
                    action: "StockAdjustRejected",
                    entityType: "StockAdjustmentRequest",
                    entityId: req.StockAdjustmentRequestId.ToString(),
                    data: new
                    {
                        req.StockId,
                        req.DeltaQty,
                        req.Reason,
                        req.ReviewNote,
                        req.ReviewedByUserId
                    }
                );
            }
            catch { }

            TempData["Ok"] = "Adjustment request rejected.";
            return RedirectToLocal(returnUrl, nameof(AdjustmentRequests));
        }

        private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(fallbackAction);
        }

        private Guid? GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private bool StockExists(Guid id)
        {
            return _context.Stocks.Any(e => e.StockId == id);
        }
    }
}

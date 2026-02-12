using System;
using System.Linq;
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
        public async Task<IActionResult> Index(string? q)
        {
            q = (q ?? "").Trim();
            ViewData["q"] = q;

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

            var data = await query
                .OrderBy(x => x.Warehouse!.Name)
                .ThenBy(x => x.Item!.SKU)
                .ToListAsync();

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
                    ModelState.AddModelError("", "Stock row already exists for this Item and Warehouse. Please edit existing stock instead.");
                }
                else
                {
                    stock.StockId = Guid.NewGuid();
                    _context.Add(stock);

                    try
                    {
                        await _context.SaveChangesAsync();
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

            var stock = await _context.Stocks.FindAsync(id);
            if (stock == null) return NotFound();

            ViewData["ItemId"] = new SelectList(_context.Items.OrderBy(x => x.SKU), "ItemId", "SKU", stock.ItemId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses.OrderBy(x => x.Name), "WarehouseId", "Name", stock.WarehouseId);
            return View(stock);
        }

        // POST: Stocks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("StockId,ItemId,WarehouseId,Quantity")] Stock stock)
        {
            if (id != stock.StockId) return NotFound();

            if (ModelState.IsValid)
            {
                var conflict = await _context.Stocks.AnyAsync(x =>
                    x.StockId != stock.StockId &&
                    x.ItemId == stock.ItemId &&
                    x.WarehouseId == stock.WarehouseId);

                if (conflict)
                {
                    ModelState.AddModelError("", "Another stock row already exists for this Item and Warehouse.");
                }
                else
                {
                    try
                    {
                        _context.Update(stock);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        if (!StockExists(stock.StockId)) return NotFound();
                        throw;
                    }
                    catch (DbUpdateException)
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again.");
                    }
                }
            }

            ViewData["ItemId"] = new SelectList(_context.Items.OrderBy(x => x.SKU), "ItemId", "SKU", stock.ItemId);
            ViewData["WarehouseId"] = new SelectList(_context.Warehouses.OrderBy(x => x.Name), "WarehouseId", "Name", stock.WarehouseId);
            return View(stock);
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

            return View(vm);
        }

        // POST: Stocks/Adjust
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(StockAdjustVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.DeltaQty == 0)
            {
                ModelState.AddModelError(nameof(vm.DeltaQty), "Delta must not be zero.");
                return View(vm);
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
                return View(vm);
            }

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

        private bool StockExists(Guid id)
        {
            return _context.Stocks.Any(e => e.StockId == id);
        }
    }
}
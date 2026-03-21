using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models;
using RetailERP.Services;

namespace RetailERP.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ItemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Items
        public async Task<IActionResult> Index(string? q, bool? active = null, string sort = "sku", string dir = "asc", int page = 1, int pageSize = 20)
        {
            q = (q ?? "").Trim();
            ViewData["q"] = q;
            ViewData["active"] = active;
            ViewData["sort"] = sort;
            ViewData["dir"] = dir;
            ViewData["page"] = page;
            ViewData["pageSize"] = pageSize;

            var query = _context.Items
                .AsNoTracking()
                .Include(x => x.Unit)
                .Include(x => x.Category)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.SKU.Contains(q) || x.Name.Contains(q));

            if (active is true)
                query = query.Where(x => x.IsActive);
            else if (active is false)
                query = query.Where(x => !x.IsActive);

            var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sort?.ToLowerInvariant() switch
            {
                "name" => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
                "price" => ascending ? query.OrderBy(x => x.UnitPrice) : query.OrderByDescending(x => x.UnitPrice),
                "category" => ascending ? query.OrderBy(x => x.Category!.Name) : query.OrderByDescending(x => x.Category!.Name),
                "status" => ascending ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
                _ => ascending ? query.OrderBy(x => x.SKU) : query.OrderByDescending(x => x.SKU)
            };

            if (page < 1) page = 1;
            if (pageSize is < 10 or > 200) pageSize = 20;

            var total = await query.CountAsync();
            var data = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewData["total"] = total;
            ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
            ViewData["to"] = Math.Min(page * pageSize, total);
            ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);
            return View(data);
        }

        // GET: Items/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .AsNoTracking()
                .Include(x => x.Unit)
                .Include(x => x.Category)
                .FirstOrDefaultAsync(m => m.ItemId == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: Items/Create
        public async Task<IActionResult> Create()
        {
            await PopulateLookupsAsync(unitId: null, categoryId: null);
            return View(new Item { IsActive = true });
        }

        // POST: Items/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ItemId,SKU,Name,UnitPrice,ReorderLevel,IsActive,Barcode,UnitId,CategoryId,MRP,PurchasePrice,GstPercent,HsnCode")] Item item)
        {
            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(item.UnitId, item.CategoryId);
                return View(item);
            }

            item.ItemId = Guid.NewGuid();
            _context.Add(item);

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Item.SKU), "SKU must be unique.");
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                    ModelState.AddModelError(nameof(Item.Barcode), "Barcode must be unique.");

                await PopulateLookupsAsync(item.UnitId, item.CategoryId);
                return View(item);
            }
        }

        // GET: Items/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.FindAsync(id);
            if (item == null) return NotFound();

            await PopulateLookupsAsync(item.UnitId, item.CategoryId);

            return View(item);
        }

        // POST: Items/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ItemId,SKU,Name,UnitPrice,ReorderLevel,IsActive,Barcode,UnitId,CategoryId,MRP,PurchasePrice,GstPercent,HsnCode")] Item item)
        {
            if (id != item.ItemId) return NotFound();
            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync(item.UnitId, item.CategoryId);
                return View(item);
            }

            var existing = await _context.Items.FindAsync(id);
            if (existing == null) return NotFound();

            // Update only the fields exposed by the current form.
            // This avoids overwriting newer fields (Barcode/Unit/Category/etc.) with nulls.
            existing.SKU = item.SKU;
            existing.Name = item.Name;
            existing.UnitPrice = item.UnitPrice;
            existing.ReorderLevel = item.ReorderLevel;
            existing.IsActive = item.IsActive;
            existing.Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode;
            existing.UnitId = item.UnitId;
            existing.CategoryId = item.CategoryId;
            existing.MRP = item.MRP;
            existing.PurchasePrice = item.PurchasePrice;
            existing.GstPercent = item.GstPercent;
            existing.HsnCode = string.IsNullOrWhiteSpace(item.HsnCode) ? null : item.HsnCode;

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(id)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Item.SKU), "SKU must be unique.");
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                    ModelState.AddModelError(nameof(Item.Barcode), "Barcode must be unique.");

                await PopulateLookupsAsync(item.UnitId, item.CategoryId);
                return View(item);
            }
        }

        private async Task PopulateLookupsAsync(Guid? unitId, Guid? categoryId)
        {
            var units = await _context.Units
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var categories = await _context.Categories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewData["UnitId"] = new SelectList(units, "UnitId", "Name", unitId);
            ViewData["CategoryId"] = new SelectList(categories, "CategoryId", "Name", categoryId);
        }

        // GET: Items/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .AsNoTracking()
                .Include(x => x.Unit)
                .Include(x => x.Category)
                .FirstOrDefaultAsync(m => m.ItemId == id);

            if (item == null) return NotFound();

            var hasReferences = await _context.Stocks
                .AsNoTracking()
                .AnyAsync(s => s.ItemId == item.ItemId);

            if (!hasReferences)
                hasReferences = await _context.InvoiceLines.AsNoTracking().AnyAsync(x => x.ItemId == item.ItemId);

            if (!hasReferences)
                hasReferences = await _context.PurchaseLines.AsNoTracking().AnyAsync(x => x.ItemId == item.ItemId);

            if (!hasReferences)
                hasReferences = await _context.StockTransactions.AsNoTracking().AnyAsync(x => x.ItemId == item.ItemId);

            if (!hasReferences)
                hasReferences = await _context.StockMovements.AsNoTracking().AnyAsync(x => x.ItemId == item.ItemId);

            ViewData["HasReferences"] = hasReferences;

            return View(item);
        }

        // POST: Items/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var hasReferences = await _context.Stocks
                .AsNoTracking()
                .AnyAsync(s => s.ItemId == id);

            if (!hasReferences)
                hasReferences = await _context.InvoiceLines.AsNoTracking().AnyAsync(x => x.ItemId == id);

            if (!hasReferences)
                hasReferences = await _context.PurchaseLines.AsNoTracking().AnyAsync(x => x.ItemId == id);

            if (!hasReferences)
                hasReferences = await _context.StockTransactions.AsNoTracking().AnyAsync(x => x.ItemId == id);

            if (!hasReferences)
                hasReferences = await _context.StockMovements.AsNoTracking().AnyAsync(x => x.ItemId == id);

            if (hasReferences)
            {
                TempData["Err"] = "Cannot delete this item because it has transactions/stock. Deactivate it instead (Edit → IsActive = false).";
                return RedirectToAction(nameof(Delete), new { id });
            }

            var item = await _context.Items.FindAsync(id);
            if (item != null)
            {
                _context.Items.Remove(item);
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Ok"] = "Item deleted.";
                }
                catch (DbUpdateException)
                {
                    TempData["Err"] = "Cannot delete this item because it is referenced by other data. Deactivate it instead (Edit → IsActive = false).";
                    return RedirectToAction(nameof(Delete), new { id });
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ItemExists(Guid id)
        {
            return _context.Items.Any(e => e.ItemId == id);
        }

        [HttpGet]
        public async Task<IActionResult> LowStock()
        {
            // Same rule as Background Jobs / StockAlertWorker / dashboard (includes items with no Stock rows = 0 on-hand)
            var rows = await LowStockReporting.Query(_context)
                .OrderBy(x => x.OnHand)
                .Select(x => new LowStockRow
                {
                    ItemId = x.ItemId,
                    SKU = x.SKU,
                    Name = x.Name,
                    OnHand = x.OnHand,
                    ReorderLevel = x.ReorderLevel
                })
                .ToListAsync();

            return View(rows);
        }

        public sealed class LowStockRow
        {
            public Guid ItemId { get; set; }
            public string SKU { get; set; } = "";
            public string Name { get; set; } = "";
            public decimal OnHand { get; set; }
            public decimal ReorderLevel { get; set; }
        }
    }
}
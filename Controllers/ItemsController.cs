using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models;

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
        public async Task<IActionResult> Index(string? q)
        {
            q = (q ?? "").Trim();
            ViewData["q"] = q;

            var query = _context.Items.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.SKU.Contains(q) || x.Name.Contains(q));

            var data = await query.OrderBy(x => x.SKU).ToListAsync();
            return View(data);
        }

        // GET: Items/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ItemId == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: Items/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Items/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ItemId,SKU,Name,UnitPrice,ReorderLevel,IsActive")] Item item)
        {
            if (!ModelState.IsValid) return View(item);

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
                return View(item);
            }
        }

        // GET: Items/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items.FindAsync(id);
            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Items/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("ItemId,SKU,Name,UnitPrice,ReorderLevel,IsActive")] Item item)
        {
            if (id != item.ItemId) return NotFound();
            if (!ModelState.IsValid) return View(item);

            try
            {
                _context.Update(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(item.ItemId)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Item.SKU), "SKU must be unique.");
                return View(item);
            }
        }

        // GET: Items/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var item = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ItemId == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // POST: Items/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var item = await _context.Items.FindAsync(id);
            if (item != null)
            {
                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
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
            var rows = await _context.Stocks
                .AsNoTracking()
                .GroupBy(s => s.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    Qty = g.Sum(x => x.Quantity)
                })
                .Join(_context.Items.AsNoTracking(),
                    s => s.ItemId,
                    i => i.ItemId,
                    (s, i) => new LowStockRow
                    {
                        ItemId = i.ItemId,
                        SKU = i.SKU,
                        Name = i.Name,
                        OnHand = s.Qty,
                        ReorderLevel = i.ReorderLevel
                    })
                .Where(x => x.ReorderLevel > 0 && x.OnHand <= x.ReorderLevel)
                .OrderBy(x => x.OnHand)
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
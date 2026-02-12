using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class WarehousesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarehousesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Warehouses
        public async Task<IActionResult> Index(string? q)
        {
            q = (q ?? "").Trim();
            ViewData["q"] = q;

            var query = _context.Warehouses.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.Name.Contains(q) || (x.Address != null && x.Address.Contains(q)));

            var data = await query.OrderBy(x => x.Name).ToListAsync();
            return View(data);
        }

        // GET: Warehouses/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.WarehouseId == id);

            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        // GET: Warehouses/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Warehouses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("WarehouseId,Name,Address")] Warehouse warehouse)
        {
            if (!ModelState.IsValid) return View(warehouse);

            warehouse.WarehouseId = Guid.NewGuid();
            _context.Add(warehouse);

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Warehouse.Name), "Warehouse Name must be unique.");
                return View(warehouse);
            }
        }

        // GET: Warehouses/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        // POST: Warehouses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("WarehouseId,Name,Address")] Warehouse warehouse)
        {
            if (id != warehouse.WarehouseId) return NotFound();
            if (!ModelState.IsValid) return View(warehouse);

            try
            {
                _context.Update(warehouse);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WarehouseExists(warehouse.WarehouseId)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Warehouse.Name), "Warehouse Name must be unique.");
                return View(warehouse);
            }
        }

        // GET: Warehouses/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.WarehouseId == id);

            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        // POST: Warehouses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse != null)
            {
                _context.Warehouses.Remove(warehouse);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool WarehouseExists(Guid id)
        {
            return _context.Warehouses.Any(e => e.WarehouseId == id);
        }
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public async Task<IActionResult> Index(string? q, string sort = "name", string dir = "asc", int page = 1, int pageSize = 20)
        {
            q = (q ?? "").Trim();
            if (page < 1) page = 1;
            if (pageSize is < 10 or > 200) pageSize = 20;

            ViewData["q"] = q;
            ViewData["sort"] = sort;
            ViewData["dir"] = dir;
            ViewData["page"] = page;
            ViewData["pageSize"] = pageSize;

            var query = _context.Warehouses
                .AsNoTracking()
                .Include(x => x.Store)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(x => x.Name.Contains(q) || (x.Address != null && x.Address.Contains(q)));

            var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            query = sort?.ToLowerInvariant() switch
            {
                "store" => ascending
                    ? query.OrderBy(x => x.Store!.StoreCode).ThenBy(x => x.Name)
                    : query.OrderByDescending(x => x.Store!.StoreCode).ThenByDescending(x => x.Name),
                "address" => ascending ? query.OrderBy(x => x.Address) : query.OrderByDescending(x => x.Address),
                _ => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name)
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

        // GET: Warehouses/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .Include(x => x.Store)
                .FirstOrDefaultAsync(m => m.WarehouseId == id);

            if (warehouse == null) return NotFound();

            return View(warehouse);
        }

        // GET: Warehouses/Create
        public async Task<IActionResult> Create()
        {
            await PopulateStoresAsync(storeId: null);
            return View();
        }

        // POST: Warehouses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("WarehouseId,Name,Address,StoreId")] Warehouse warehouse)
        {
            if (!ModelState.IsValid)
            {
                await PopulateStoresAsync(warehouse.StoreId);
                return View(warehouse);
            }

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
                await PopulateStoresAsync(warehouse.StoreId);
                return View(warehouse);
            }
        }

        // GET: Warehouses/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses.FindAsync(id);
            if (warehouse == null) return NotFound();

            await PopulateStoresAsync(warehouse.StoreId);
            return View(warehouse);
        }

        // POST: Warehouses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("WarehouseId,Name,Address,StoreId")] Warehouse warehouse)
        {
            if (id != warehouse.WarehouseId) return NotFound();
            if (!ModelState.IsValid)
            {
                await PopulateStoresAsync(warehouse.StoreId);
                return View(warehouse);
            }

            var existing = await _context.Warehouses.FindAsync(id);
            if (existing == null) return NotFound();

            // Update only the fields exposed by the current form.
            // This avoids overwriting StoreId with nulls.
            existing.Name = warehouse.Name;
            existing.Address = warehouse.Address;
            existing.StoreId = warehouse.StoreId;

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!WarehouseExists(id)) return NotFound();
                throw;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(Warehouse.Name), "Warehouse Name must be unique.");
                await PopulateStoresAsync(warehouse.StoreId);
                return View(warehouse);
            }
        }

        // GET: Warehouses/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var warehouse = await _context.Warehouses
                .AsNoTracking()
                .Include(x => x.Store)
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

        private async Task PopulateStoresAsync(Guid? storeId)
        {
            var stores = await _context.Stores
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.StoreCode)
                .ToListAsync();

            ViewData["StoreId"] = new SelectList(
                items: stores,
                dataValueField: nameof(Store.StoreId),
                dataTextField: nameof(Store.Name),
                selectedValue: storeId);
        }
    }
}
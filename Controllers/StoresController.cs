using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class StoresController : Controller
{
    private readonly ApplicationDbContext _db;

    public StoresController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, bool? active = null, string sort = "code", string dir = "asc", int page = 1, int pageSize = 20)
    {
        q = (q ?? string.Empty).Trim();
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        ViewData["q"] = q;
        ViewData["active"] = active;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.Stores.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.StoreCode.Contains(q) || x.Name.Contains(q) || (x.City != null && x.City.Contains(q)));

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "name" => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
            "city" => ascending ? query.OrderBy(x => x.City) : query.OrderByDescending(x => x.City),
            "status" => ascending ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
            _ => ascending ? query.OrderBy(x => x.StoreCode) : query.OrderByDescending(x => x.StoreCode),
        };

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewData["total"] = total;
        ViewData["totalPages"] = totalPages < 1 ? 1 : totalPages;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        return View(rows);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null) return NotFound();

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == id);
        if (store is null) return NotFound();

        var warehouseCount = await _db.Warehouses.AsNoTracking().CountAsync(w => w.StoreId == id);
        ViewData["WarehouseCount"] = warehouseCount;

        return View(store);
    }

    [HttpGet]
    public IActionResult Create() => View(new Store());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StoreId,StoreCode,Name,Address,Phone,City,State,GstNo,PanNo,IsActive")] Store store)
    {
        if (!ModelState.IsValid) return View(store);

        store.StoreId = Guid.NewGuid();
        _db.Stores.Add(store);

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Store created.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Store.StoreCode), "Store code must be unique.");
            return View(store);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();
        var store = await _db.Stores.FindAsync(id);
        if (store is null) return NotFound();
        return View(store);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("StoreId,StoreCode,Name,Address,Phone,City,State,GstNo,PanNo,IsActive")] Store store)
    {
        if (id != store.StoreId) return NotFound();
        if (!ModelState.IsValid) return View(store);

        var existing = await _db.Stores.FindAsync(id);
        if (existing is null) return NotFound();

        existing.StoreCode = store.StoreCode;
        existing.Name = store.Name;
        existing.Address = store.Address;
        existing.Phone = store.Phone;
        existing.City = store.City;
        existing.State = store.State;
        existing.GstNo = store.GstNo;
        existing.PanNo = store.PanNo;
        existing.IsActive = store.IsActive;

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Store updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Store.StoreCode), "Store code must be unique.");
            return View(store);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null) return NotFound();

        var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == id);
        if (store is null) return NotFound();

        var warehouseCount = await _db.Warehouses.AsNoTracking().CountAsync(w => w.StoreId == id);
        ViewData["WarehouseCount"] = warehouseCount;

        return View(store);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var store = await _db.Stores.FindAsync(id);
        if (store is null) return RedirectToAction(nameof(Index));

        var warehouseCount = await _db.Warehouses.AnyAsync(w => w.StoreId == id);
        if (warehouseCount)
        {
            TempData["Err"] = "Cannot delete store because warehouses are linked to it.";
            return RedirectToAction(nameof(Index));
        }

        _db.Stores.Remove(store);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Store deleted.";
        return RedirectToAction(nameof(Index));
    }
}

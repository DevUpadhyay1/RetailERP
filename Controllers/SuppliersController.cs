using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _db;

    public SuppliersController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, bool? active = null, string sort = "name", string dir = "asc", int page = 1, int pageSize = 20)
    {
        q = (q ?? string.Empty).Trim();
        ViewData["q"] = q;
        ViewData["active"] = active;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || (x.Phone != null && x.Phone.Contains(q)) || (x.Email != null && x.Email.Contains(q)));

        if (active is true)
            query = query.Where(x => x.IsActive);
        else if (active is false)
            query = query.Where(x => !x.IsActive);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "active" => ascending ? query.OrderBy(x => x.IsActive).ThenBy(x => x.Name) : query.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name),
            _ => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name)
        };

        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewData["total"] = total;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);
        return View(rows);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null) return NotFound();

        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == id);
        if (supplier is null) return NotFound();

        return View(supplier);
    }

    [HttpGet]
    public IActionResult Create() => View(new Supplier());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("SupplierId,Name,Phone,Email,Address,IsActive")] Supplier supplier)
    {
        if (!ModelState.IsValid) return View(supplier);

        supplier.SupplierId = Guid.NewGuid();
        _db.Suppliers.Add(supplier);

        try
        {
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Supplier.Name), "Supplier name must be unique.");
            return View(supplier);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return NotFound();
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("SupplierId,Name,Phone,Email,Address,IsActive")] Supplier supplier)
    {
        if (id != supplier.SupplierId) return NotFound();
        if (!ModelState.IsValid) return View(supplier);

        try
        {
            _db.Update(supplier);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Supplier.Name), "Supplier name must be unique.");
            return View(supplier);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null) return NotFound();

        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == id);
        if (supplier is null) return NotFound();

        return View(supplier);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return RedirectToAction(nameof(Index));

        var hasPurchases = await _db.Purchases.AnyAsync(p => p.SupplierId == id);
        if (hasPurchases)
        {
            TempData["Err"] = "Cannot delete supplier because purchases exist.";
            return RedirectToAction(nameof(Index));
        }

        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Supplier deleted.";
        return RedirectToAction(nameof(Index));
    }
}

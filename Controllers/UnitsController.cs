using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class UnitsController : Controller
{
    private readonly ApplicationDbContext _db;

    public UnitsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, bool? active = null, string sort = "name", string dir = "asc", int page = 1, int pageSize = 20)
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

        var query = _db.Units.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || (x.Symbol != null && x.Symbol.Contains(q)));

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "symbol" => ascending ? query.OrderBy(x => x.Symbol) : query.OrderByDescending(x => x.Symbol),
            "status" => ascending ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
            _ => ascending ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
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
        var unit = await _db.Units.AsNoTracking().FirstOrDefaultAsync(x => x.UnitId == id);
        if (unit is null) return NotFound();

        var itemCount = await _db.Items.AsNoTracking().CountAsync(i => i.UnitId == id);
        ViewData["ItemCount"] = itemCount;

        return View(unit);
    }

    [HttpGet]
    public IActionResult Create() => View(new Unit());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("UnitId,Name,Symbol,IsActive")] Unit unit)
    {
        if (!ModelState.IsValid) return View(unit);

        unit.UnitId = Guid.NewGuid();
        _db.Units.Add(unit);

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Unit created.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Unit.Name), "Unit name must be unique.");
            return View(unit);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();
        var unit = await _db.Units.FindAsync(id);
        if (unit is null) return NotFound();
        return View(unit);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("UnitId,Name,Symbol,IsActive")] Unit unit)
    {
        if (id != unit.UnitId) return NotFound();
        if (!ModelState.IsValid) return View(unit);

        var existing = await _db.Units.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = unit.Name;
        existing.Symbol = unit.Symbol;
        existing.IsActive = unit.IsActive;

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Unit updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Unit.Name), "Unit name must be unique.");
            return View(unit);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null) return NotFound();
        var unit = await _db.Units.AsNoTracking().FirstOrDefaultAsync(x => x.UnitId == id);
        if (unit is null) return NotFound();

        var itemCount = await _db.Items.AsNoTracking().CountAsync(i => i.UnitId == id);
        ViewData["ItemCount"] = itemCount;

        return View(unit);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var unit = await _db.Units.FindAsync(id);
        if (unit is null) return RedirectToAction(nameof(Index));

        var hasItems = await _db.Items.AnyAsync(i => i.UnitId == id);
        if (hasItems)
        {
            TempData["Err"] = "Cannot delete unit because items are linked to it.";
            return RedirectToAction(nameof(Index));
        }

        _db.Units.Remove(unit);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Unit deleted.";
        return RedirectToAction(nameof(Index));
    }
}

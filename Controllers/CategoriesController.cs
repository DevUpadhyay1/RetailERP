using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;

    public CategoriesController(ApplicationDbContext db)
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

        var query = _db.Categories
            .AsNoTracking()
            .Include(x => x.ParentCategory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || (x.ParentCategory != null && x.ParentCategory.Name.Contains(q)));

        if (active.HasValue)
            query = query.Where(x => x.IsActive == active.Value);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "parent" => ascending
                ? query.OrderBy(x => x.ParentCategory!.Name).ThenBy(x => x.Name)
                : query.OrderByDescending(x => x.ParentCategory!.Name).ThenByDescending(x => x.Name),
            "status" => ascending ? query.OrderBy(x => x.IsActive).ThenBy(x => x.Name) : query.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.Name),
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

        var category = await _db.Categories
            .AsNoTracking()
            .Include(x => x.ParentCategory)
            .FirstOrDefaultAsync(x => x.CategoryId == id);
        if (category is null) return NotFound();

        var childCount = await _db.Categories.AsNoTracking().CountAsync(c => c.ParentCategoryId == id);
        var itemCount = await _db.Items.AsNoTracking().CountAsync(i => i.CategoryId == id);
        ViewData["ChildCount"] = childCount;
        ViewData["ItemCount"] = itemCount;

        return View(category);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateParentOptionsAsync(null);
        return View(new Category());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("CategoryId,Name,ParentCategoryId,IsActive")] Category category)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentOptionsAsync(category.ParentCategoryId);
            return View(category);
        }

        category.CategoryId = Guid.NewGuid();
        _db.Categories.Add(category);

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Category.Name), "Category name must be unique.");
            await PopulateParentOptionsAsync(category.ParentCategoryId);
            return View(category);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null) return NotFound();

        var category = await _db.Categories.FindAsync(id);
        if (category is null) return NotFound();

        await PopulateParentOptionsAsync(category.ParentCategoryId, excludeId: category.CategoryId);
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("CategoryId,Name,ParentCategoryId,IsActive")] Category category)
    {
        if (id != category.CategoryId) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateParentOptionsAsync(category.ParentCategoryId, excludeId: category.CategoryId);
            return View(category);
        }

        var existing = await _db.Categories.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = category.Name;
        existing.ParentCategoryId = category.ParentCategoryId;
        existing.IsActive = category.IsActive;

        try
        {
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Category.Name), "Category name must be unique.");
            await PopulateParentOptionsAsync(category.ParentCategoryId, excludeId: category.CategoryId);
            return View(category);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null) return NotFound();

        var category = await _db.Categories
            .AsNoTracking()
            .Include(x => x.ParentCategory)
            .FirstOrDefaultAsync(x => x.CategoryId == id);
        if (category is null) return NotFound();

        var childCount = await _db.Categories.AsNoTracking().CountAsync(c => c.ParentCategoryId == id);
        var itemCount = await _db.Items.AsNoTracking().CountAsync(i => i.CategoryId == id);
        ViewData["ChildCount"] = childCount;
        ViewData["ItemCount"] = itemCount;

        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category is null) return RedirectToAction(nameof(Index));

        var hasChildren = await _db.Categories.AnyAsync(c => c.ParentCategoryId == id);
        if (hasChildren)
        {
            TempData["Err"] = "Cannot delete category because child categories exist.";
            return RedirectToAction(nameof(Index));
        }

        var hasItems = await _db.Items.AnyAsync(i => i.CategoryId == id);
        if (hasItems)
        {
            TempData["Err"] = "Cannot delete category because items are linked to it.";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateParentOptionsAsync(Guid? selectedId, Guid? excludeId = null)
    {
        var query = _db.Categories.AsNoTracking().Where(c => c.IsActive);
        if (excludeId.HasValue)
            query = query.Where(c => c.CategoryId != excludeId.Value);

        var rows = await query.OrderBy(c => c.Name).ToListAsync();
        var options = rows.Select(c => new SelectListItem(c.Name, c.CategoryId.ToString()));
        ViewData["ParentCategoryId"] = new SelectList(options, "Value", "Text", selectedId?.ToString());
    }
}

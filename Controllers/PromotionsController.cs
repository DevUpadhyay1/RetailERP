using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class PromotionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PromotionsController(ApplicationDbContext db) => _db = db;

    // ── List ──
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string sort = "ValidTo", string dir = "desc", int page = 1)
    {
        const int pageSize = 20;
        var query = _db.Promotions.AsNoTracking()
            .Include(p => p.Item)
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || (p.Description != null && p.Description.Contains(q)));

        query = sort switch
        {
            "Name" => dir == "asc" ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            "PromoType" => dir == "asc" ? query.OrderBy(p => p.PromoType) : query.OrderByDescending(p => p.PromoType),
            "Priority" => dir == "asc" ? query.OrderBy(p => p.Priority) : query.OrderByDescending(p => p.Priority),
            _ => dir == "asc" ? query.OrderBy(p => p.ValidTo) : query.OrderByDescending(p => p.ValidTo),
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Q = q;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);

        return View(items);
    }

    // ── Create ──
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadDropdownsAsync();
        return View(new Promotion());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Promotion promo)
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return View(promo);
        }

        promo.PromotionId = Guid.NewGuid();
        _db.Promotions.Add(promo);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Promotion '{promo.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promo is null) return NotFound();
        await LoadDropdownsAsync();
        return View(promo);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Promotion model)
    {
        if (!ModelState.IsValid)
        {
            await LoadDropdownsAsync();
            return View(model);
        }

        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promo is null) return NotFound();

        promo.Name = model.Name;
        promo.Description = model.Description;
        promo.PromoType = model.PromoType;
        promo.DiscountPercent = model.DiscountPercent;
        promo.DiscountAmount = model.DiscountAmount;
        promo.ItemId = model.ItemId;
        promo.CategoryId = model.CategoryId;
        promo.BuyQty = model.BuyQty;
        promo.GetQty = model.GetQty;
        promo.FreeItemId = model.FreeItemId;
        promo.ComboItemIds = model.ComboItemIds;
        promo.ComboPrice = model.ComboPrice;
        promo.ValidFrom = model.ValidFrom;
        promo.ValidTo = model.ValidTo;
        promo.HappyHourStart = model.HappyHourStart;
        promo.HappyHourEnd = model.HappyHourEnd;
        promo.MinBillAmount = model.MinBillAmount;
        promo.MaxUsesTotal = model.MaxUsesTotal;
        promo.Priority = model.Priority;
        promo.IsExclusive = model.IsExclusive;
        promo.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Promotion updated.";
        return RedirectToAction(nameof(Index));
    }

    // ── Details ──
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var promo = await _db.Promotions.AsNoTracking()
            .Include(p => p.Item)
            .Include(p => p.Category)
            .Include(p => p.FreeItem)
            .FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promo is null) return NotFound();
        return View(promo);
    }

    // ── Toggle Active ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promo is null) return NotFound();

        promo.IsActive = !promo.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = promo.IsActive ? "Promotion activated." : "Promotion deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // ── Delete ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionId == id);
        if (promo is null) return NotFound();

        _db.Promotions.Remove(promo);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Promotion deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ── AJAX: Get active promotions for POS ──
    [HttpGet]
    public async Task<IActionResult> ActiveList()
    {
        var today = DateTime.Today;
        var promos = await _db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.ValidFrom <= today && p.ValidTo >= today)
            .OrderBy(p => p.Priority)
            .Select(p => new
            {
                p.PromotionId,
                p.Name,
                p.PromoType,
                p.DiscountPercent,
                p.DiscountAmount,
                ItemName = p.Item != null ? p.Item.Name : null,
                CategoryName = p.Category != null ? p.Category.Name : null,
                p.ValidTo
            })
            .ToListAsync();

        return Json(promos);
    }

    private async Task LoadDropdownsAsync()
    {
        ViewBag.Items = new SelectList(
            await _db.Items.AsNoTracking().Where(i => i.IsActive).OrderBy(i => i.Name)
                .Select(i => new { i.ItemId, Display = i.Name + " (" + i.SKU + ")" }).ToListAsync(),
            "ItemId", "Display");

        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking().OrderBy(c => c.Name)
                .Select(c => new { c.CategoryId, c.Name }).ToListAsync(),
            "CategoryId", "Name");
    }
}

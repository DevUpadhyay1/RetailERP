using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager")]
public class CouponsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CouponService _coupons;

    public CouponsController(ApplicationDbContext db, CouponService coupons)
    {
        _db = db;
        _coupons = coupons;
    }

    // ── List ──
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string sort = "ValidTo", string dir = "desc", int page = 1)
    {
        const int pageSize = 20;
        var query = _db.Coupons.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Code.Contains(q) || (c.Description != null && c.Description.Contains(q)));

        query = sort switch
        {
            "Code" => dir == "asc" ? query.OrderBy(c => c.Code) : query.OrderByDescending(c => c.Code),
            "DiscountType" => dir == "asc" ? query.OrderBy(c => c.DiscountType) : query.OrderByDescending(c => c.DiscountType),
            "UsedCount" => dir == "asc" ? query.OrderBy(c => c.UsedCount) : query.OrderByDescending(c => c.UsedCount),
            _ => dir == "asc" ? query.OrderBy(c => c.ValidTo) : query.OrderByDescending(c => c.ValidTo),
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
    public IActionResult Create() => View(new Coupon());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Coupon coupon)
    {
        if (!ModelState.IsValid) return View(coupon);

        coupon.CouponId = Guid.NewGuid();
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Coupon {coupon.Code} created.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == id);
        if (coupon is null) return NotFound();
        return View(coupon);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Coupon model)
    {
        if (!ModelState.IsValid) return View(model);

        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == id);
        if (coupon is null) return NotFound();

        coupon.Code = model.Code;
        coupon.Description = model.Description;
        coupon.DiscountType = model.DiscountType;
        coupon.DiscountValue = model.DiscountValue;
        coupon.MinBillAmount = model.MinBillAmount;
        coupon.MaxDiscount = model.MaxDiscount;
        coupon.ValidFrom = model.ValidFrom;
        coupon.ValidTo = model.ValidTo;
        coupon.MaxUses = model.MaxUses;
        coupon.IsActive = model.IsActive;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Coupon updated.";
        return RedirectToAction(nameof(Index));
    }

    // ── Details + usage history ──
    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var coupon = await _db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CouponId == id);
        if (coupon is null) return NotFound();

        ViewBag.Usages = await _db.CouponUsages.AsNoTracking()
            .Include(u => u.PosBill)
            .Where(u => u.CouponId == id)
            .OrderByDescending(u => u.UsedAtUtc)
            .Take(50)
            .ToListAsync();

        return View(coupon);
    }

    // ── Toggle active ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == id);
        if (coupon is null) return NotFound();

        coupon.IsActive = !coupon.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = coupon.IsActive ? "Coupon activated." : "Coupon deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // ── AJAX: Validate coupon (used by POS screen) ──
    [HttpGet]
    public async Task<IActionResult> Validate(string code, decimal subTotal)
    {
        var result = await _coupons.ValidateAsync(code, subTotal);
        return Json(result);
    }
}

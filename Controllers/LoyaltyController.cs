using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier")]
public class LoyaltyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LoyaltyService _loyalty;

    public LoyaltyController(ApplicationDbContext db, LoyaltyService loyalty)
    {
        _db = db;
        _loyalty = loyalty;
    }

    // ── List all loyalty cards ──
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string sort = "JoinDate", string dir = "desc", int page = 1)
    {
        const int pageSize = 20;
        var query = _db.LoyaltyCards.AsNoTracking().Include(c => c.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c =>
                c.CardNumber.Contains(q) ||
                (c.Customer != null && (c.Customer.Name.Contains(q) || (c.Customer.Phone != null && c.Customer.Phone.Contains(q)))));
        }

        query = sort switch
        {
            "CardNumber" => dir == "asc" ? query.OrderBy(c => c.CardNumber) : query.OrderByDescending(c => c.CardNumber),
            "Customer" => dir == "asc" ? query.OrderBy(c => c.Customer!.Name) : query.OrderByDescending(c => c.Customer!.Name),
            "Points" => dir == "asc" ? query.OrderBy(c => c.PointsBalance) : query.OrderByDescending(c => c.PointsBalance),
            "Tier" => dir == "asc" ? query.OrderBy(c => c.Tier) : query.OrderByDescending(c => c.Tier),
            _ => dir == "asc" ? query.OrderBy(c => c.JoinDate) : query.OrderByDescending(c => c.JoinDate),
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

    // ── Create loyalty card ──
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Only show customers who don't already have a card
        var existingIds = await _db.LoyaltyCards.Select(c => c.CustomerId).ToListAsync();
        ViewBag.Customers = new SelectList(
            await _db.Customers.Where(c => !existingIds.Contains(c.CustomerId)).OrderBy(c => c.Name).ToListAsync(),
            "CustomerId", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid customerId)
    {
        try
        {
            var card = await _loyalty.CreateCardAsync(customerId);
            TempData["Success"] = $"Loyalty card {card.CardNumber} created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create));
        }
    }

    // ── Card details + transaction history ──
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, int page = 1)
    {
        const int pageSize = 20;
        var card = await _db.LoyaltyCards.AsNoTracking()
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.LoyaltyCardId == id);

        if (card is null) return NotFound();

        var txQuery = _db.LoyaltyTransactions.AsNoTracking()
            .Where(t => t.LoyaltyCardId == id)
            .OrderByDescending(t => t.OccurredAtUtc);

        var total = await txQuery.CountAsync();
        var transactions = await txQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.Card = card;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.TierName = LoyaltyService.GetTierName(card.Tier);

        return View(transactions);
    }

    // ── Toggle active/inactive ──
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        var card = await _db.LoyaltyCards.FirstOrDefaultAsync(c => c.LoyaltyCardId == id);
        if (card is null) return NotFound();

        card.IsActive = !card.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = card.IsActive ? "Card activated." : "Card deactivated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ── AJAX: Lookup card by phone/card number (used by POS screen) ──
    [HttpGet]
    public async Task<IActionResult> Lookup(string code)
    {
        var card = await _loyalty.LookupAsync(code);
        if (card is null) return Json(new { success = false, message = "Card not found." });
        return Json(new
        {
            success = true,
            card = new
            {
                card.LoyaltyCardId,
                card.CardNumber,
                card.PointsBalance,
                card.Tier,
                TierName = LoyaltyService.GetTierName(card.Tier),
                CustomerName = card.Customer?.Name,
                CustomerPhone = card.Customer?.Phone
            }
        });
    }
}

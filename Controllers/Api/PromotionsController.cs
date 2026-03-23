using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager")]
public class PromotionsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public PromotionsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? active,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Data.Entities.Promotion> query = _db.Promotions.AsNoTracking()
            .Include(p => p.Item).Include(p => p.Category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term)
                                  || (p.Description != null && p.Description.ToLower().Contains(term)));
        }
        if (active.HasValue) query = query.Where(p => p.IsActive == active.Value);

        var total = await query.CountAsync();
        var items = await query.OrderBy(p => p.Priority).ThenByDescending(p => p.ValidTo)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<PromotionDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await _db.Promotions.AsNoTracking()
            .Include(x => x.Item).Include(x => x.Category).Include(x => x.FreeItem)
            .FirstOrDefaultAsync(x => x.PromotionId == id);
        if (p is null) return NotFound(ApiResponse<object>.Fail("Promotion not found."));
        return Ok(ApiResponse<PromotionDto>.Ok(MapToDto(p)));
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var today = DateTime.Today;
        var promos = await _db.Promotions.AsNoTracking()
            .Include(p => p.Item).Include(p => p.Category)
            .Where(p => p.IsActive && p.ValidFrom <= today && p.ValidTo >= today)
            .OrderBy(p => p.Priority)
            .ToListAsync();

        return Ok(ApiResponse<List<PromotionDto>>.Ok(promos.Select(MapToDto).ToList()));
    }

    private static PromotionDto MapToDto(Data.Entities.Promotion p) => new()
    {
        PromotionId = p.PromotionId,
        Name = p.Name,
        Description = p.Description,
        PromoType = p.PromoType,
        DiscountPercent = p.DiscountPercent,
        DiscountAmount = p.DiscountAmount,
        ItemId = p.ItemId,
        ItemName = p.Item?.Name,
        CategoryId = p.CategoryId,
        CategoryName = p.Category?.Name,
        BuyQty = p.BuyQty,
        GetQty = p.GetQty,
        FreeItemId = p.FreeItemId,
        FreeItemName = p.FreeItem?.Name,
        ValidFrom = p.ValidFrom,
        ValidTo = p.ValidTo,
        HappyHourStart = p.HappyHourStart,
        HappyHourEnd = p.HappyHourEnd,
        MinBillAmount = p.MinBillAmount,
        MaxUsesTotal = p.MaxUsesTotal,
        UsedCount = p.UsedCount,
        Priority = p.Priority,
        IsExclusive = p.IsExclusive,
        IsActive = p.IsActive
    };
}

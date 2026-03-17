using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

/// <summary>
/// Read-only API for POS bills. Actual POS operations stay in MVC controller.
/// </summary>
public class PosController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public PosController(ApplicationDbContext db) => _db = db;

    [HttpGet("bills")]
    public async Task<IActionResult> GetBills([FromQuery] string? search, [FromQuery] byte? status,
                                               [FromQuery] Guid? storeId, [FromQuery] DateTime? from,
                                               [FromQuery] DateTime? to,
                                               [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.PosBills.Include(b => b.Store).Include(b => b.Customer).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(b => b.BillNo.ToLower().Contains(term));
        }
        if (status.HasValue) q = q.Where(b => b.Status == status.Value);
        if (storeId.HasValue) q = q.Where(b => b.StoreId == storeId.Value);
        if (from.HasValue) q = q.Where(b => b.BillDate >= from.Value);
        if (to.HasValue) q = q.Where(b => b.BillDate <= to.Value);

        var total = await q.CountAsync();
        var list = await q.OrderByDescending(b => b.BillDate).ThenByDescending(b => b.CreatedAtUtc)
                          .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<PosBillDto>
        {
            Data = list.Select(b => new PosBillDto
            {
                PosBillId = b.PosBillId, BillNo = b.BillNo, BillDate = b.BillDate,
                StoreId = b.StoreId, StoreName = b.Store?.Name,
                CustomerId = b.CustomerId, CustomerName = b.Customer?.Name,
                SubTotal = b.SubTotal, TaxTotal = b.TaxTotal,
                DiscountTotal = b.DiscountTotal, GrandTotal = b.GrandTotal, Status = b.Status
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("bills/{id:guid}")]
    public async Task<IActionResult> GetBill(Guid id)
    {
        var b = await _db.PosBills
            .Include(x => x.Store).Include(x => x.Customer)
            .Include(x => x.Lines).ThenInclude(l => l.Item)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PosBillId == id);

        if (b is null) return NotFound(ApiResponse<object>.Fail("Bill not found."));

        var dto = new PosBillDto
        {
            PosBillId = b.PosBillId, BillNo = b.BillNo, BillDate = b.BillDate,
            StoreId = b.StoreId, StoreName = b.Store?.Name,
            CustomerId = b.CustomerId, CustomerName = b.Customer?.Name,
            SubTotal = b.SubTotal, TaxTotal = b.TaxTotal,
            DiscountTotal = b.DiscountTotal, GrandTotal = b.GrandTotal, Status = b.Status,
            Lines = b.Lines.Select(l => new PosBillLineDto
            {
                PosBillLineId = l.PosBillLineId, ItemId = l.ItemId,
                ItemName = l.ItemNameSnapshot ?? l.Item?.Name,
                SKU = l.SkuSnapshot ?? l.Item?.SKU,
                Qty = l.Qty, UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount, LineTotal = l.LineTotal
            }).ToList()
        };

        return Ok(ApiResponse<PosBillDto>.Ok(dto));
    }
}

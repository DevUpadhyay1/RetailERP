using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

public class StocksController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public StocksController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] Guid? warehouseId,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Stocks.Include(s => s.Item).Include(s => s.Warehouse).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(s => s.Item!.Name.ToLower().Contains(term) ||
                             (s.Item.SKU != null && s.Item.SKU.ToLower().Contains(term)));
        }
        if (warehouseId.HasValue) q = q.Where(s => s.WarehouseId == warehouseId.Value);

        var total = await q.CountAsync();
        var list = await q.OrderBy(s => s.Item!.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<StockDto>
        {
            Data = list.Select(MapToDto).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var s = await _db.Stocks.Include(x => x.Item).Include(x => x.Warehouse)
                         .AsNoTracking().FirstOrDefaultAsync(x => x.StockId == id);
        if (s is null) return NotFound(ApiResponse<object>.Fail("Stock record not found."));
        return Ok(ApiResponse<StockDto>.Ok(MapToDto(s)));
    }

    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));

        var stock = await _db.Stocks.FirstOrDefaultAsync(s => s.ItemId == dto.ItemId && s.WarehouseId == dto.WarehouseId);

        if (stock is null)
        {
            // Create a new stock record if none exists
            stock = new Stock { ItemId = dto.ItemId, WarehouseId = dto.WarehouseId, Quantity = 0 };
            _db.Stocks.Add(stock);
        }

        stock.Quantity += dto.AdjustmentQty;
        if (stock.Quantity < 0) stock.Quantity = 0;

        // Record the movement
        _db.StockMovements.Add(new StockMovement
        {
            ItemId = dto.ItemId,
            WarehouseId = dto.WarehouseId,
            QuantityChange = dto.AdjustmentQty,
            MovementType = dto.AdjustmentQty >= 0 ? "AdjustIn" : "AdjustOut",
            Notes = dto.Reason ?? "API adjustment"
        });

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok($"Stock adjusted. New quantity: {stock.Quantity}"));
    }

    private static StockDto MapToDto(Stock s) => new()
    {
        StockId = s.StockId, ItemId = s.ItemId,
        ItemName = s.Item?.Name ?? string.Empty,
        SKU = s.Item?.SKU,
        WarehouseId = s.WarehouseId,
        WarehouseName = s.Warehouse?.Name ?? string.Empty,
        Quantity = s.Quantity
    };
}

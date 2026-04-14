using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;
using RetailERP.Services;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory")]
public class ItemsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public ItemsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? active,
                                             [FromQuery] Guid? categoryId, [FromQuery] int page = 1,
                                             [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Item> q = _db.Items.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(i =>
                i.Name.StartsWith(term) ||
                i.SKU.StartsWith(term) ||
                i.Barcode == term);
        }
        if (active.HasValue) q = q.Where(i => i.IsActive == active.Value);
        if (categoryId.HasValue) q = q.Where(i => i.CategoryId == categoryId.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemDto
            {
                ItemId = i.ItemId,
                SKU = i.SKU,
                Name = i.Name,
                Barcode = i.Barcode,
                UnitPrice = i.UnitPrice,
                MRP = i.MRP,
                PurchasePrice = i.PurchasePrice,
                GstPercent = i.GstPercent,
                HsnCode = i.HsnCode,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive,
                UnitId = i.UnitId,
                UnitName = i.Unit != null ? i.Unit.Name : null,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null
            })
            .ToListAsync();

        return Ok(new PagedResponse<ItemDto>
        {
            Data = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await _db.Items.AsNoTracking()
            .Where(i => i.ItemId == id)
            .Select(i => new ItemDto
            {
                ItemId = i.ItemId,
                SKU = i.SKU,
                Name = i.Name,
                Barcode = i.Barcode,
                UnitPrice = i.UnitPrice,
                MRP = i.MRP,
                PurchasePrice = i.PurchasePrice,
                GstPercent = i.GstPercent,
                HsnCode = i.HsnCode,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive,
                UnitId = i.UnitId,
                UnitName = i.Unit != null ? i.Unit.Name : null,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null
            })
            .FirstOrDefaultAsync();
        if (item is null) return NotFound(ApiResponse<object>.Fail("Item not found."));
        return Ok(ApiResponse<ItemDto>.Ok(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ItemCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));

        var entity = new Item
        {
            SKU = dto.SKU,
            Name = dto.Name,
            Barcode = dto.Barcode,
            UnitPrice = dto.UnitPrice,
            MRP = dto.MRP,
            PurchasePrice = dto.PurchasePrice,
            GstPercent = dto.GstPercent,
            HsnCode = dto.HsnCode,
            ReorderLevel = dto.ReorderLevel,
            UnitId = dto.UnitId,
            CategoryId = dto.CategoryId
        };

        _db.Items.Add(entity);
        await _db.SaveChangesAsync();

        var created = await _db.Items.AsNoTracking()
            .Where(i => i.ItemId == entity.ItemId)
            .Select(i => new ItemDto
            {
                ItemId = i.ItemId,
                SKU = i.SKU,
                Name = i.Name,
                Barcode = i.Barcode,
                UnitPrice = i.UnitPrice,
                MRP = i.MRP,
                PurchasePrice = i.PurchasePrice,
                GstPercent = i.GstPercent,
                HsnCode = i.HsnCode,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive,
                UnitId = i.UnitId,
                UnitName = i.Unit != null ? i.Unit.Name : null,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null
            })
            .FirstAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.ItemId }, ApiResponse<ItemDto>.Ok(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ItemUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));

        var entity = await _db.Items.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Item not found."));
        if (entity.CompanyId != GetCompanyId()) return Forbid();

        entity.SKU = dto.SKU; entity.Name = dto.Name; entity.Barcode = dto.Barcode;
        entity.UnitPrice = dto.UnitPrice; entity.MRP = dto.MRP; entity.PurchasePrice = dto.PurchasePrice;
        entity.GstPercent = dto.GstPercent; entity.HsnCode = dto.HsnCode; entity.ReorderLevel = dto.ReorderLevel;
        entity.UnitId = dto.UnitId; entity.CategoryId = dto.CategoryId; entity.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Item updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Items.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Item not found."));
        if (entity.CompanyId != GetCompanyId()) return Forbid();

        _db.Items.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Item deleted."));
    }

    [HttpGet("low-stock")]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = "Identity.Application,Bearer")]
    public async Task<IActionResult> LowStock([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var lowQ = LowStockReporting.Query(_db);
        var total = await lowQ.CountAsync();
        var slice = await lowQ.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        if (slice.Count == 0)
        {
            return Ok(new PagedResponse<object>
            {
                Data = new List<object>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            });
        }

        var ids = slice.Select(x => x.ItemId).ToList();
        var items = await _db.Items.AsNoTracking()
            .Where(i => ids.Contains(i.ItemId))
            .Select(i => new ItemDto
            {
                ItemId = i.ItemId,
                SKU = i.SKU,
                Name = i.Name,
                Barcode = i.Barcode,
                UnitPrice = i.UnitPrice,
                MRP = i.MRP,
                PurchasePrice = i.PurchasePrice,
                GstPercent = i.GstPercent,
                HsnCode = i.HsnCode,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive,
                UnitId = i.UnitId,
                UnitName = i.Unit != null ? i.Unit.Name : null,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null
            })
            .ToDictionaryAsync(i => i.ItemId);
        var onHandById = slice.ToDictionary(x => x.ItemId, x => x.OnHand);

        var data = slice
            .Where(x => items.ContainsKey(x.ItemId))
            .Select(x =>
            {
                var item = items[x.ItemId];
                return (object)new
                {
                    Item = item,
                    CurrentStock = onHandById[x.ItemId],
                    item.ReorderLevel
                };
            })
            .ToList();

        return Ok(new PagedResponse<object>
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    private static ItemDto MapToDto(Item i) => new()
    {
        ItemId = i.ItemId,
        SKU = i.SKU,
        Name = i.Name,
        Barcode = i.Barcode,
        UnitPrice = i.UnitPrice,
        MRP = i.MRP,
        PurchasePrice = i.PurchasePrice,
        GstPercent = i.GstPercent,
        HsnCode = i.HsnCode,
        ReorderLevel = i.ReorderLevel,
        IsActive = i.IsActive,
        UnitId = i.UnitId,
        UnitName = i.Unit?.Name,
        CategoryId = i.CategoryId,
        CategoryName = i.Category?.Name
    };
}

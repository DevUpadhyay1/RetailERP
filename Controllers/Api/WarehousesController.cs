using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory")]
public class WarehousesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public WarehousesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] Guid? storeId,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Warehouse> q = _db.Warehouses.Include(w => w.Store).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(w => w.Name.ToLower().Contains(term) || (w.Address != null && w.Address.ToLower().Contains(term)));
        }
        if (storeId.HasValue) q = q.Where(w => w.StoreId == storeId.Value);

        var total = await q.CountAsync();
        var list = await q.OrderBy(w => w.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<WarehouseDto>
        {
            Data = list.Select(MapToDto).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var w = await _db.Warehouses.Include(x => x.Store).AsNoTracking().FirstOrDefaultAsync(x => x.WarehouseId == id);
        if (w is null) return NotFound(ApiResponse<object>.Fail("Warehouse not found."));
        return Ok(ApiResponse<WarehouseDto>.Ok(MapToDto(w)));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WarehouseCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Warehouse { Name = dto.Name, Address = dto.Address, StoreId = dto.StoreId };
        _db.Warehouses.Add(entity);
        await _db.SaveChangesAsync();
        await _db.Entry(entity).Reference(e => e.Store).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.WarehouseId }, ApiResponse<WarehouseDto>.Ok(MapToDto(entity)));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] WarehouseCreateDto dto)
    {
        var entity = await _db.Warehouses.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Warehouse not found."));
        entity.Name = dto.Name; entity.Address = dto.Address; entity.StoreId = dto.StoreId;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Warehouse updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Warehouses.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Warehouse not found."));
        _db.Warehouses.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Warehouse deleted."));
    }

    private static WarehouseDto MapToDto(Warehouse w) => new()
    {
        WarehouseId = w.WarehouseId, Name = w.Name, Address = w.Address,
        StoreId = w.StoreId, StoreName = w.Store?.Name
    };
}

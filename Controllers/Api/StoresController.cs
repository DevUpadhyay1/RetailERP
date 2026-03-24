using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory")]
public class StoresController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public StoresController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? active,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Store> q = _db.Stores.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(s => s.Name.ToLower().Contains(term) || s.StoreCode.ToLower().Contains(term)
                           || (s.City != null && s.City.ToLower().Contains(term)));
        }
        if (active.HasValue) q = q.Where(s => s.IsActive == active.Value);

        var total = await q.CountAsync();
        var list = await q.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<StoreDto>
        {
            Data = list.Select(s => new StoreDto
            {
                StoreId = s.StoreId,
                StoreCode = s.StoreCode,
                Name = s.Name,
                Address = s.Address,
                Phone = s.Phone,
                City = s.City,
                State = s.State,
                GstNo = s.GstNo,
                IsActive = s.IsActive
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var s = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.StoreId == id);
        if (s is null) return NotFound(ApiResponse<object>.Fail("Store not found."));
        return Ok(ApiResponse<StoreDto>.Ok(new StoreDto
        {
            StoreId = s.StoreId,
            StoreCode = s.StoreCode,
            Name = s.Name,
            Address = s.Address,
            Phone = s.Phone,
            City = s.City,
            State = s.State,
            GstNo = s.GstNo,
            IsActive = s.IsActive
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StoreCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Store
        {
            StoreCode = dto.StoreCode,
            Name = dto.Name,
            Address = dto.Address,
            Phone = dto.Phone,
            City = dto.City,
            State = dto.State,
            GstNo = dto.GstNo
        };
        _db.Stores.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.StoreId },
            ApiResponse<StoreDto>.Ok(new StoreDto
            {
                StoreId = entity.StoreId,
                StoreCode = entity.StoreCode,
                Name = entity.Name,
                Address = entity.Address,
                Phone = entity.Phone,
                City = entity.City,
                State = entity.State,
                GstNo = entity.GstNo,
                IsActive = true
            }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] StoreCreateDto dto)
    {
        var entity = await _db.Stores.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Store not found."));
        entity.StoreCode = dto.StoreCode; entity.Name = dto.Name; entity.Address = dto.Address;
        entity.Phone = dto.Phone; entity.City = dto.City; entity.State = dto.State; entity.GstNo = dto.GstNo;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Store updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Stores.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Store not found."));
        _db.Stores.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Store deleted."));
    }
}

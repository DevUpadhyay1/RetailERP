using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory")]
public class UnitsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public UnitsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await _db.Units.AsNoTracking().CountAsync();
        var list = await _db.Units.AsNoTracking().OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new PagedResponse<UnitDto>
        {
            Data = list.Select(u => new UnitDto { UnitId = u.UnitId, Name = u.Name, Symbol = u.Symbol, IsActive = u.IsActive }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var u = await _db.Units.AsNoTracking().FirstOrDefaultAsync(x => x.UnitId == id);
        if (u is null) return NotFound(ApiResponse<object>.Fail("Unit not found."));
        return Ok(ApiResponse<UnitDto>.Ok(new UnitDto { UnitId = u.UnitId, Name = u.Name, Symbol = u.Symbol, IsActive = u.IsActive }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnitCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Unit { Name = dto.Name, Symbol = dto.Symbol };
        _db.Units.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.UnitId },
            ApiResponse<UnitDto>.Ok(new UnitDto { UnitId = entity.UnitId, Name = entity.Name, Symbol = entity.Symbol, IsActive = true }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UnitCreateDto dto)
    {
        var entity = await _db.Units.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Unit not found."));
        entity.Name = dto.Name; entity.Symbol = dto.Symbol;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Unit updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Units.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Unit not found."));
        _db.Units.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Unit deleted."));
    }
}

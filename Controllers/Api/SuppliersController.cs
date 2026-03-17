using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

public class SuppliersController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public SuppliersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? active,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Supplier> q = _db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(s => s.Name.ToLower().Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
        }
        if (active.HasValue) q = q.Where(s => s.IsActive == active.Value);

        var total = await q.CountAsync();
        var list = await q.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<SupplierDto>
        {
            Data = list.Select(s => new SupplierDto
            {
                SupplierId = s.SupplierId, Name = s.Name, Phone = s.Phone, Email = s.Email,
                Address = s.Address, IsActive = s.IsActive
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == id);
        if (s is null) return NotFound(ApiResponse<object>.Fail("Supplier not found."));
        return Ok(ApiResponse<SupplierDto>.Ok(new SupplierDto
        {
            SupplierId = s.SupplierId, Name = s.Name, Phone = s.Phone, Email = s.Email,
            Address = s.Address, IsActive = s.IsActive
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SupplierCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Supplier { Name = dto.Name, Phone = dto.Phone, Email = dto.Email, Address = dto.Address };
        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.SupplierId },
            ApiResponse<SupplierDto>.Ok(new SupplierDto
            {
                SupplierId = entity.SupplierId, Name = entity.Name, Phone = entity.Phone,
                Email = entity.Email, Address = entity.Address, IsActive = true
            }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SupplierCreateDto dto)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Supplier not found."));
        entity.Name = dto.Name; entity.Phone = dto.Phone; entity.Email = dto.Email; entity.Address = dto.Address;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Supplier updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Suppliers.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Supplier not found."));
        _db.Suppliers.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Supplier deleted."));
    }
}

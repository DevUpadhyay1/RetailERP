using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Cashier")]
public class CustomersController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public CustomersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Customer> q = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(term)
                           || (c.Phone != null && c.Phone.Contains(term))
                           || (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var total = await q.CountAsync();
        var list = await q.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<CustomerDto>
        {
            Data = list.Select(c => new CustomerDto { CustomerId = c.CustomerId, Name = c.Name, Phone = c.Phone, Email = c.Email }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == id);
        if (c is null) return NotFound(ApiResponse<object>.Fail("Customer not found."));
        return Ok(ApiResponse<CustomerDto>.Ok(new CustomerDto { CustomerId = c.CustomerId, Name = c.Name, Phone = c.Phone, Email = c.Email }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Customer { Name = dto.Name, Phone = dto.Phone, Email = dto.Email };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.CustomerId },
            ApiResponse<CustomerDto>.Ok(new CustomerDto { CustomerId = entity.CustomerId, Name = entity.Name, Phone = entity.Phone, Email = entity.Email }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerCreateDto dto)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Customer not found."));
        entity.Name = dto.Name; entity.Phone = dto.Phone; entity.Email = dto.Email;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Customer updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Customer not found."));
        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Customer deleted."));
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory")]
public class CategoriesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public CategoriesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        IQueryable<Category> q = _db.Categories.AsNoTracking().Include(c => c.ParentCategory);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.Name.ToLower().Contains(search.Trim().ToLower()));

        var total = await q.CountAsync();
        var list = await q.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<CategoryDto>
        {
            Data = list.Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                IsActive = c.IsActive,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory?.Name
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var c = await _db.Categories.AsNoTracking().Include(x => x.ParentCategory).FirstOrDefaultAsync(x => x.CategoryId == id);
        if (c is null) return NotFound(ApiResponse<object>.Fail("Category not found."));
        return Ok(ApiResponse<CategoryDto>.Ok(new CategoryDto
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            IsActive = c.IsActive,
            ParentCategoryId = c.ParentCategoryId,
            ParentCategoryName = c.ParentCategory?.Name
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryCreateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ApiResponse<object>.Fail("Validation failed."));
        var entity = new Category { Name = dto.Name, ParentCategoryId = dto.ParentCategoryId };
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.CategoryId },
            ApiResponse<CategoryDto>.Ok(new CategoryDto { CategoryId = entity.CategoryId, Name = entity.Name, IsActive = true }));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryCreateDto dto)
    {
        var entity = await _db.Categories.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Category not found."));
        entity.Name = dto.Name;
        entity.ParentCategoryId = dto.ParentCategoryId;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Category updated."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.Categories.FindAsync(id);
        if (entity is null) return NotFound(ApiResponse<object>.Fail("Category not found."));
        _db.Categories.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Category deleted."));
    }
}

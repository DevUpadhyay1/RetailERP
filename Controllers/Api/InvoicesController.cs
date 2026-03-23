using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Finance")]
public class InvoicesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public InvoicesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] byte? status,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Invoices.AsNoTracking()
            .Include(i => i.Customer).Include(i => i.Warehouse).Include(i => i.Employee);

        IQueryable<Data.Entities.Invoice> query = q;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i => i.InvoiceNo.ToLower().Contains(term)
                                  || (i.Customer != null && i.Customer.Name.ToLower().Contains(term)));
        }
        if (status.HasValue) query = query.Where(i => i.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.InvoiceNo)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<InvoiceDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var inv = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer).Include(i => i.Warehouse).Include(i => i.Employee)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (inv is null) return NotFound(ApiResponse<object>.Fail("Invoice not found."));

        var dto = MapToDto(inv);
        dto.Lines = inv.Lines.OrderBy(l => l.Item?.SKU).Select(l => new InvoiceLineDto
        {
            InvoiceLineId = l.InvoiceLineId,
            ItemId = l.ItemId,
            ItemName = l.Item != null ? $"{l.Item.SKU} - {l.Item.Name}" : l.ItemNameSnapshot ?? "",
            Qty = l.Qty,
            UnitPrice = l.UnitPrice,
            DiscountAmount = l.DiscountAmount,
            LineTotal = l.Qty * l.UnitPrice - l.DiscountAmount
        }).ToList();

        return Ok(ApiResponse<InvoiceDto>.Ok(dto));
    }

    private static InvoiceDto MapToDto(Data.Entities.Invoice i) => new()
    {
        InvoiceId = i.InvoiceId,
        InvoiceNo = i.InvoiceNo,
        InvoiceDate = i.InvoiceDate,
        CustomerId = i.CustomerId,
        CustomerName = i.Customer?.Name,
        WarehouseId = i.WarehouseId,
        WarehouseName = i.Warehouse?.Name,
        EmployeeName = i.Employee != null ? $"{i.Employee.FirstName} {i.Employee.LastName}" : null,
        TotalAmount = i.TotalAmount,
        Status = i.Status,
        StatusName = i.Status switch { 1 => "Draft", 2 => "Posted", _ => "Unknown" },
        PostedAt = i.PostedAt
    };
}

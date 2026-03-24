using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Inventory,Finance")]
public class PurchasesController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public PurchasesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] byte? status,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Purchases.AsNoTracking()
            .Include(p => p.Supplier).Include(p => p.Warehouse).Include(p => p.Employee);

        IQueryable<Data.Entities.Purchase> query = q;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.PurchaseNo.ToLower().Contains(term)
                                  || (p.Supplier != null && p.Supplier.Name.ToLower().Contains(term)));
        }
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.PurchaseNo)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new PagedResponse<PurchaseDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await _db.Purchases.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Employee)
            .Include(x => x.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(x => x.PurchaseId == id);
        if (p is null) return NotFound(ApiResponse<object>.Fail("Purchase not found."));

        var dto = MapToDto(p);
        dto.Lines = p.Lines.OrderBy(l => l.Item?.SKU).Select(l => new PurchaseLineDto
        {
            PurchaseLineId = l.PurchaseLineId,
            ItemId = l.ItemId,
            ItemName = l.Item != null ? $"{l.Item.SKU} - {l.Item.Name}" : l.ItemNameSnapshot ?? "",
            Qty = l.Qty,
            UnitCost = l.UnitCost,
            LineTotal = l.Qty * l.UnitCost
        }).ToList();

        return Ok(ApiResponse<PurchaseDto>.Ok(dto));
    }

    private static PurchaseDto MapToDto(Data.Entities.Purchase p) => new()
    {
        PurchaseId = p.PurchaseId,
        PurchaseNo = p.PurchaseNo,
        PurchaseDate = p.PurchaseDate,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier?.Name,
        WarehouseId = p.WarehouseId,
        WarehouseName = p.Warehouse?.Name,
        EmployeeName = p.Employee != null ? $"{p.Employee.FirstName} {p.Employee.LastName}" : null,
        TotalAmount = p.TotalAmount,
        Status = p.Status,
        StatusName = p.Status switch { 1 => "Draft", 2 => "Received", _ => "Unknown" },
        ReceivedAt = p.ReceivedAt,
        Notes = p.Notes
    };
}

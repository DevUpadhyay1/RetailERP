using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;
using System.ComponentModel.DataAnnotations;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory,Finance")]
public class PurchasesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseService _purchaseService;
    private readonly ItemOnboardingService _itemOnboarding;

    public PurchasesController(ApplicationDbContext db, PurchaseService purchaseService, ItemOnboardingService itemOnboarding)
    {
        _db = db;
        _purchaseService = purchaseService;
        _itemOnboarding = itemOnboarding;
    }

    public async Task<IActionResult> Index(string? q, byte? status = null, string sort = "date", string dir = "desc", int page = 1, int pageSize = 20)
    {
        q = (q ?? string.Empty).Trim();
        ViewData["q"] = q;
        ViewData["status"] = status;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.Purchases
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.PurchaseNo.Contains(q) || (x.Supplier != null && x.Supplier.Name.Contains(q)));

        if (status is not null)
            query = query.Where(x => x.Status == status);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "no" => ascending ? query.OrderBy(x => x.PurchaseNo) : query.OrderByDescending(x => x.PurchaseNo),
            "total" => ascending ? query.OrderBy(x => x.TotalAmount) : query.OrderByDescending(x => x.TotalAmount),
            "status" => ascending ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
            _ => ascending ? query.OrderBy(x => x.PurchaseDate).ThenBy(x => x.PurchaseNo) : query.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.PurchaseNo)
        };

        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        var total = await query.CountAsync();
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewData["total"] = total;
        ViewData["from"] = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        ViewData["to"] = Math.Min(page * pageSize, total);
        ViewData["totalPages"] = (int)Math.Ceiling(total / (double)pageSize);

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new PurchaseCreateVm { PurchaseDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var purchaseId = await _purchaseService.CreateDraftAsync(vm.SupplierId, vm.WarehouseId, vm.PurchaseDate, vm.EmployeeId);
        return RedirectToAction(nameof(Edit), new { id = purchaseId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var purchase = await _db.Purchases
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.Employee)
            .Include(x => x.Lines)
                .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(x => x.PurchaseId == id);

        if (purchase is null) return NotFound();

        ViewBag.Items = new SelectList(
            await _db.Items.AsNoTracking().OrderBy(x => x.SKU).Select(x => new { x.ItemId, Text = x.SKU + " - " + x.Name }).ToListAsync(),
            "ItemId",
            "Text"
        );

        var vm = new PurchaseEditVm
        {
            PurchaseId = purchase.PurchaseId,
            PurchaseNo = purchase.PurchaseNo,
            PurchaseDate = purchase.PurchaseDate,
            SupplierName = purchase.Supplier?.Name ?? "(Not set)",
            WarehouseName = purchase.Warehouse?.Name ?? "(Not set)",
            EmployeeName = purchase.Employee is null
                ? "-"
                : $"{purchase.Employee.EmployeeCode} - {purchase.Employee.FirstName} {purchase.Employee.LastName}",
            Status = purchase.Status,
            ReceivedAt = purchase.ReceivedAt,
            TotalAmount = purchase.TotalAmount,
            Lines = purchase.Lines
                .OrderBy(x => x.Item!.SKU)
                .Select(x => new PurchaseLineRowVm
                {
                    PurchaseLineId = x.PurchaseLineId,
                    ItemName = x.Item == null ? "" : $"{x.Item.SKU} - {x.Item.Name}",
                    Qty = x.Qty,
                    UnitCost = x.UnitCost
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(AddPurchaseLineVm vm)
    {
        try
        {
            await _purchaseService.AddLineAsync(vm.PurchaseId, vm.ItemId, vm.Qty, vm.UnitCost);
            return RedirectToAction(nameof(Edit), new { id = vm.PurchaseId });
        }
        catch (Exception ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = vm.PurchaseId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreateItem(QuickCreatePurchaseItemVm vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Err"] = "Quick add failed. SKU and Name are required.";
            return RedirectToAction(nameof(Edit), new { id = vm.PurchaseId });
        }

        var req = new ItemOnboardingService.QuickCreateItemRequest
        {
            SKU = vm.SKU.Trim(),
            Name = vm.Name.Trim(),
            Barcode = vm.Barcode,
            UnitPrice = vm.UnitPrice,
            MRP = vm.MRP,
            PurchasePrice = vm.UnitCost,
            GstPercent = vm.GstPercent,
            HsnCode = vm.HsnCode,
            ReorderLevel = vm.ReorderLevel,
            UnitName = vm.UnitName,
            CategoryName = vm.CategoryName
        };

        var result = await _itemOnboarding.QuickCreateItemAsync(req);
        if (!result.Success)
        {
            TempData["Err"] = result.Message;
            return RedirectToAction(nameof(Edit), new { id = vm.PurchaseId });
        }

        TempData["Ok"] = result.Message + " You can now add it in purchase lines.";
        return RedirectToAction(nameof(Edit), new { id = vm.PurchaseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(Guid purchaseLineId, Guid purchaseId)
    {
        await _purchaseService.RemoveLineAsync(purchaseLineId);
        return RedirectToAction(nameof(Edit), new { id = purchaseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(Guid purchaseId)
    {
        try
        {
            await _purchaseService.ReceiveAsync(purchaseId);
            TempData["Ok"] = "Purchase received and stock increased.";
            return RedirectToAction(nameof(Edit), new { id = purchaseId });
        }
        catch (Exception ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = purchaseId });
        }
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Suppliers = new SelectList(
            await _db.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            "SupplierId",
            "Name"
        );

        ViewBag.Warehouses = new SelectList(
            await _db.Warehouses.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            "WarehouseId",
            "Name"
        );

        ViewBag.Employees = new SelectList(
            await _db.Employees
                .AsNoTracking()
                .OrderBy(x => x.EmployeeCode)
                .Select(x => new { x.EmployeeId, Name = x.EmployeeCode + " - " + x.FirstName + " " + x.LastName })
                .ToListAsync(),
            "EmployeeId",
            "Name"
        );
    }

    public sealed class PurchaseCreateVm
    {
        public Guid SupplierId { get; set; }
        public Guid WarehouseId { get; set; }
        public DateTime PurchaseDate { get; set; }

        public Guid? EmployeeId { get; set; }
    }

    public sealed class PurchaseEditVm
    {
        public Guid PurchaseId { get; set; }
        public string PurchaseNo { get; set; } = "";
        public DateTime PurchaseDate { get; set; }
        public string SupplierName { get; set; } = "";
        public string WarehouseName { get; set; } = "";
        public string EmployeeName { get; set; } = "-";
        public byte Status { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public List<PurchaseLineRowVm> Lines { get; set; } = new();
    }

    public sealed class PurchaseLineRowVm
    {
        public Guid PurchaseLineId { get; set; }
        public string ItemName { get; set; } = "";
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Qty * UnitCost;
    }

    public sealed class AddPurchaseLineVm
    {
        public Guid PurchaseId { get; set; }
        public Guid ItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
    }

    public sealed class QuickCreatePurchaseItemVm
    {
        public Guid PurchaseId { get; set; }

        [Required]
        [StringLength(50)]
        public string SKU { get; set; } = "";

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = "";

        [StringLength(50)]
        public string? Barcode { get; set; }

        [Range(0, 999999999)]
        public decimal UnitCost { get; set; }

        [Range(0, 999999999)]
        public decimal UnitPrice { get; set; }

        [Range(0, 999999999)]
        public decimal? MRP { get; set; }

        [Range(0, 100)]
        public decimal? GstPercent { get; set; }

        [StringLength(20)]
        public string? HsnCode { get; set; }

        [Range(0, 999999)]
        public int ReorderLevel { get; set; }

        [StringLength(50)]
        public string? UnitName { get; set; }

        [StringLength(100)]
        public string? CategoryName { get; set; }
    }
}

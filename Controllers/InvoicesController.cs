using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;

namespace RetailERP.Controllers;
[Authorize(Roles = "Admin,Manager,Cashier,Finance")]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly InvoiceService _invoiceService;

    public InvoicesController(ApplicationDbContext db, InvoiceService invoiceService)
    {
        _db = db;
        _invoiceService = invoiceService;
    }

    // Finance -> Invoice Register (Step 5)
    public async Task<IActionResult> Index(string? q, byte? status = null, string sort = "date", string dir = "desc", int page = 1, int pageSize = 20)
    {
        q = (q ?? string.Empty).Trim();
        ViewData["q"] = q;
        ViewData["status"] = status;
        ViewData["sort"] = sort;
        ViewData["dir"] = dir;
        ViewData["page"] = page;
        ViewData["pageSize"] = pageSize;

        var query = _db.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.InvoiceNo.Contains(q) || (x.Customer != null && x.Customer.Name.Contains(q)));

        if (status is not null)
            query = query.Where(x => x.Status == status);

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "no" => ascending ? query.OrderBy(x => x.InvoiceNo) : query.OrderByDescending(x => x.InvoiceNo),
            "total" => ascending ? query.OrderBy(x => x.TotalAmount) : query.OrderByDescending(x => x.TotalAmount),
            "status" => ascending ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status),
            _ => ascending ? query.OrderBy(x => x.InvoiceDate).ThenBy(x => x.InvoiceNo) : query.OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceNo)
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
        return View(new InvoiceCreateVm { InvoiceDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateVm vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var invoiceId = await _invoiceService.CreateDraftAsync(vm.CustomerId, vm.WarehouseId, vm.InvoiceDate, vm.EmployeeId);
        return RedirectToAction(nameof(Edit), new { id = invoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Employee)
            .Include(x => x.Lines)
                .ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(x => x.InvoiceId == id);

        if (invoice is null) return NotFound();

        ViewBag.Items = new SelectList(
            await _db.Items.AsNoTracking().OrderBy(x => x.SKU).Select(x => new { x.ItemId, Text = x.SKU + " - " + x.Name }).ToListAsync(),
            "ItemId",
            "Text"
        );

        var vm = new InvoiceEditVm
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceNo = invoice.InvoiceNo,
            InvoiceDate = invoice.InvoiceDate,
            CustomerName = invoice.Customer?.Name ?? "(Not set)",
            WarehouseName = invoice.Warehouse?.Name ?? "(Not set)",
            EmployeeName = invoice.Employee is null
                ? "-"
                : $"{invoice.Employee.EmployeeCode} - {invoice.Employee.FirstName} {invoice.Employee.LastName}",
            Status = invoice.Status,
            PostedAt = invoice.PostedAt,
            TotalAmount = invoice.TotalAmount,
            Lines = invoice.Lines
                .OrderBy(x => x.Item?.SKU)
                .Select(x => new InvoiceLineRowVm
                {
                    InvoiceLineId = x.InvoiceLineId,
                    ItemName = x.Item is null ? "(Missing Item)" : $"{x.Item.SKU} - {x.Item.Name}",
                    Qty = x.Qty,
                    UnitPrice = x.UnitPrice
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(AddInvoiceLineVm vm)
    {
        try
        {
            await _invoiceService.AddLineAsync(vm.InvoiceId, vm.ItemId, vm.Qty, vm.UnitPrice);
            return RedirectToAction(nameof(Edit), new { id = vm.InvoiceId });
        }
        catch (Exception ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = vm.InvoiceId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLine(Guid invoiceLineId, Guid invoiceId)
    {
        await _invoiceService.RemoveLineAsync(invoiceLineId);
        return RedirectToAction(nameof(Edit), new { id = invoiceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(Guid invoiceId)
    {
        try
        {
            await _invoiceService.PostAsync(invoiceId);
            TempData["Ok"] = "Invoice posted and stock deducted.";
            return RedirectToAction(nameof(Edit), new { id = invoiceId });
        }
        catch (Exception ex)
        {
            TempData["Err"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id = invoiceId });
        }
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            "CustomerId",
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

    public sealed class InvoiceCreateVm
    {
        public Guid CustomerId { get; set; }
        public Guid WarehouseId { get; set; }
        public DateTime InvoiceDate { get; set; }

        public Guid? EmployeeId { get; set; }
    }

    public sealed class InvoiceEditVm
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string WarehouseName { get; set; } = "";
        public string EmployeeName { get; set; } = "-";
        public byte Status { get; set; }
        public DateTime? PostedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public List<InvoiceLineRowVm> Lines { get; set; } = new();
    }

    public sealed class InvoiceLineRowVm
    {
        public Guid InvoiceLineId { get; set; }
        public string ItemName { get; set; } = "";
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Qty * UnitPrice;
    }

    public sealed class AddInvoiceLineVm
    {
        public Guid InvoiceId { get; set; }
        public Guid ItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
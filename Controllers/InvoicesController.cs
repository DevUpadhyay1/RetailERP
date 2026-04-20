using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RetailERP.Controllers;
[Authorize(Roles = "Admin,Manager,Cashier,Finance")]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly InvoiceService _invoiceService;
    private readonly InvoicePdfService _invoicePdf;

    public InvoicesController(ApplicationDbContext db, InvoiceService invoiceService, InvoicePdfService invoicePdf)
    {
        _db = db;
        _invoiceService = invoiceService;
        _invoicePdf = invoicePdf;
    }

    private Guid GetCompanyId() =>
        Guid.Parse(User.FindFirstValue("CompanyId") ?? Guid.Empty.ToString());

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
        return View(new InvoiceCreateVm
        {
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(30),
            DocumentType = InvoiceDocumentType.TaxInvoice
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceCreateVm vm)
    {
        if (vm.CustomerId == Guid.Empty)
            ModelState.AddModelError(nameof(InvoiceCreateVm.CustomerId), "Customer is required.");

        if (vm.WarehouseId == Guid.Empty)
            ModelState.AddModelError(nameof(InvoiceCreateVm.WarehouseId), "Warehouse is required.");

        if ((vm.DocumentType == InvoiceDocumentType.CreditNote || vm.DocumentType == InvoiceDocumentType.DebitNote)
            && string.IsNullOrWhiteSpace(vm.ReferenceInvoiceNo))
        {
            ModelState.AddModelError(nameof(InvoiceCreateVm.ReferenceInvoiceNo), "Reference Invoice No is required for Credit/Debit notes.");
        }

        if (vm.DueDate.HasValue && vm.DueDate.Value.Date < vm.InvoiceDate.Date)
        {
            ModelState.AddModelError(nameof(InvoiceCreateVm.DueDate), "Due Date cannot be before Invoice Date.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var invoiceId = await _invoiceService.CreateDraftAsync(
            vm.CustomerId,
            vm.WarehouseId,
            vm.InvoiceDate,
            vm.EmployeeId,
            vm.DocumentType,
            vm.DueDate,
            vm.ReferenceInvoiceNo);
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

        var companyId = GetCompanyId();
        var templateQuery = _db.BillTemplates
            .AsNoTracking()
            .Where(t =>
                t.TemplateType == 2 &&
                t.DocumentType == invoice.DocumentType &&
                t.CompanyId == companyId);

        var preferredTemplate = await ResolveInvoiceTemplateAsync(templateQuery, invoice.Warehouse?.StoreId, invoice.BillTemplateId);

        var templates = await templateQuery
            .Include(t => t.Store)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.TemplateScope)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();

        var templateOptions = templates
            .Select(t => new SelectListItem
            {
                Value = t.BillTemplateId.ToString(),
                Text = $"{t.TemplateName} {(t.TemplateScope == InvoiceTemplateScope.Store ? $"(Store: {t.Store?.Name ?? "N/A"})" : "(Company)")}{(t.IsDefault ? " [Default]" : string.Empty)}",
                Selected = preferredTemplate != null && t.BillTemplateId == preferredTemplate.BillTemplateId
            })
            .ToList();

        ViewBag.InvoiceTemplates = templateOptions;

        var itemOptions = await _db.Items
            .AsNoTracking()
            .OrderBy(x => x.SKU)
            .Select(x => new InvoiceItemOptionVm
            {
                ItemId = x.ItemId,
                Text = x.SKU + " - " + x.Name,
                UnitPrice = x.UnitPrice
            })
            .ToListAsync();

        ViewBag.ItemOptions = itemOptions;
        ViewBag.Items = new SelectList(itemOptions, nameof(InvoiceItemOptionVm.ItemId), nameof(InvoiceItemOptionVm.Text));

        var vm = new InvoiceEditVm
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceNo = invoice.InvoiceNo,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            DocumentType = invoice.DocumentType,
            ReferenceInvoiceNo = invoice.ReferenceInvoiceNo,
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
                    ItemName = x.Item is not null
                        ? $"{x.Item.SKU} - {x.Item.Name}"
                        : string.IsNullOrWhiteSpace(x.ItemNameSnapshot) && string.IsNullOrWhiteSpace(x.ItemSkuSnapshot)
                            ? "(Missing Item)"
                            : $"{(string.IsNullOrWhiteSpace(x.ItemSkuSnapshot) ? "N/A" : x.ItemSkuSnapshot)} - {(string.IsNullOrWhiteSpace(x.ItemNameSnapshot) ? "Item" : x.ItemNameSnapshot)}",
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(Guid invoiceId, Guid? templateId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Warehouse)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null) return NotFound();

        if (templateId.HasValue)
        {
            var template = await _db.BillTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.BillTemplateId == templateId.Value);

            if (template is null)
            {
                TempData["Err"] = "Selected template does not exist.";
                return RedirectToAction(nameof(Edit), new { id = invoiceId });
            }

            var companyId = GetCompanyId();
            if (template.CompanyId != companyId || template.TemplateType != 2 || template.DocumentType != invoice.DocumentType)
            {
                TempData["Err"] = "Template is not valid for this invoice type/company.";
                return RedirectToAction(nameof(Edit), new { id = invoiceId });
            }

            if (template.TemplateScope == InvoiceTemplateScope.Store)
            {
                var invoiceStoreId = invoice.Warehouse?.StoreId;
                if (!invoiceStoreId.HasValue || template.StoreId != invoiceStoreId)
                {
                    TempData["Err"] = "Store template can only be used for invoices of the same store.";
                    return RedirectToAction(nameof(Edit), new { id = invoiceId });
                }
            }
        }

        invoice.BillTemplateId = templateId;
        invoice.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Ok"] = templateId.HasValue
            ? "Invoice template saved for this invoice."
            : "Invoice template reset to automatic fallback.";

        return RedirectToAction(nameof(Edit), new { id = invoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(Guid id, Guid? templateId = null)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice is null) return NotFound();

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == GetCompanyId());
        if (company is null) return NotFound();

        BillTemplate? template;

        var templateQuery = _db.BillTemplates
            .AsNoTracking()
            .Where(t =>
                t.TemplateType == 2 &&
                t.DocumentType == invoice.DocumentType &&
                t.CompanyId == company.CompanyId);

        template = await ResolveInvoiceTemplateAsync(templateQuery, invoice.Warehouse?.StoreId, templateId ?? invoice.BillTemplateId);

        if (template is null)
        {
            return BadRequest("No invoice template found. Please create one in Bill Templates and mark default.");
        }

        var pdf = _invoicePdf.Generate(invoice, template, company);
        var fileName = $"Invoice_{invoice.InvoiceNo}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    private static async Task<BillTemplate?> ResolveInvoiceTemplateAsync(
        IQueryable<BillTemplate> templateQuery,
        Guid? storeId,
        Guid? templateId = null)
    {
        if (templateId.HasValue)
        {
            var explicitTemplate = await templateQuery.FirstOrDefaultAsync(t => t.BillTemplateId == templateId.Value);
            if (explicitTemplate is not null)
                return explicitTemplate;
        }

        BillTemplate? template = null;

        if (storeId.HasValue)
        {
            template = await templateQuery
                .Where(t =>
                    t.TemplateScope == InvoiceTemplateScope.Store &&
                    t.StoreId == storeId.Value &&
                    t.IsDefault)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .FirstOrDefaultAsync();
        }

        template ??= await templateQuery
            .Where(t => t.TemplateScope == InvoiceTemplateScope.Company && t.IsDefault)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        if (storeId.HasValue)
        {
            template ??= await templateQuery
                .Where(t =>
                    t.TemplateScope == InvoiceTemplateScope.Store &&
                    t.StoreId == storeId.Value)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .FirstOrDefaultAsync();
        }

        template ??= await templateQuery
            .Where(t => t.TemplateScope == InvoiceTemplateScope.Company)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        return template;
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

        ViewBag.DocumentTypes = new SelectList(new[]
        {
            new { Value = (byte)InvoiceDocumentType.TaxInvoice, Text = "Tax Invoice" },
            new { Value = (byte)InvoiceDocumentType.BillOfSupply, Text = "Bill of Supply" },
            new { Value = (byte)InvoiceDocumentType.CreditNote, Text = "Credit Note" },
            new { Value = (byte)InvoiceDocumentType.DebitNote, Text = "Debit Note" },
            new { Value = (byte)InvoiceDocumentType.ProformaInvoice, Text = "Proforma Invoice" }
        }, "Value", "Text");
    }

    public sealed class InvoiceCreateVm
    {
        public Guid CustomerId { get; set; }
        public Guid WarehouseId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.TaxInvoice;
        public string? ReferenceInvoiceNo { get; set; }

        public Guid? EmployeeId { get; set; }
    }

    public sealed class InvoiceEditVm
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.TaxInvoice;
        public string? ReferenceInvoiceNo { get; set; }
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

        [Range(typeof(decimal), "0.01", "999999999")]
        public decimal Qty { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal UnitPrice { get; set; }
    }

    public sealed class InvoiceItemOptionVm
    {
        public Guid ItemId { get; set; }
        public string Text { get; set; } = "";
        public decimal UnitPrice { get; set; }
    }
}

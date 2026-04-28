using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;
using System.Security.Claims;

namespace RetailERP.Controllers;

/// <summary>Sprint 6 – Bill template designer (drag-and-drop receipt/invoice customisation).</summary>
[Authorize(Roles = "Admin,SuperAdmin")]
public class BillTemplatesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ReceiptPdfService _receiptPdf;
    private readonly InvoicePdfService _invoicePdf;

    public BillTemplatesController(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ReceiptPdfService receiptPdf,
        InvoicePdfService invoicePdf)
    {
        _db = db;
        _env = env;
        _receiptPdf = receiptPdf;
        _invoicePdf = invoicePdf;
    }

    private Guid GetCompanyId() =>
        Guid.Parse(User.FindFirstValue("CompanyId") ?? Guid.Empty.ToString());

    // ──────────────────────────────────────────────────────
    // List templates
    // ──────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var templates = await _db.BillTemplates
            .AsNoTracking()
            .Include(t => t.Store)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.TemplateType)
            .ThenBy(t => t.DocumentType)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();

        return View(templates);
    }

    // ──────────────────────────────────────────────────────
    // Create
    // ──────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create()
    {
        var template = new BillTemplate
        {
            TemplateName = "Default Receipt",
            LayoutJson = GetPresetLayoutJson("modern", 1),
            TemplateType = 1,
            DocumentType = InvoiceDocumentType.TaxInvoice,
            TemplateScope = InvoiceTemplateScope.Company
        };
        ViewBag.Preset = "modern";
        ViewBag.Stores = _db.Stores.AsNoTracking().OrderBy(s => s.Name).ToList();
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BillTemplate template, string preset = "modern")
    {
        if (template.TemplateScope == InvoiceTemplateScope.Store && template.StoreId is null)
            ModelState.AddModelError(nameof(BillTemplate.StoreId), "Store is required when template scope is Store.");

        if (!ModelState.IsValid)
        {
            ViewBag.Stores = await _db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
            return View(template);
        }

        if (string.IsNullOrWhiteSpace(template.LayoutJson) || template.LayoutJson == "[]")
            template.LayoutJson = GetPresetLayoutJson(preset, template.TemplateType);

        if (template.TemplateScope == InvoiceTemplateScope.Company)
            template.StoreId = null;

        template.BillTemplateId = Guid.NewGuid();
        template.CompanyId = GetCompanyId();
        template.CreatedAtUtc = DateTime.UtcNow;
        template.UpdatedAtUtc = DateTime.UtcNow;

        // Forcefully set as default if it's the very first template of this type
        var isFirst = !await _db.BillTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.CompanyId == template.CompanyId && t.TemplateType == template.TemplateType);
        
        if (isFirst)
        {
            template.IsDefault = true;
        }

        // If marked default, unset other defaults of same type
        if (template.IsDefault)
            await UnsetOtherDefaults(template);

        _db.BillTemplates.Add(template);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Template created.";
        return RedirectToAction(nameof(Designer), new { id = template.BillTemplateId });
    }

    // ──────────────────────────────────────────────────────
    // Designer (drag-and-drop)
    // ──────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Designer(Guid id)
    {
        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == GetCompanyId());
        ViewBag.Company = company;

        return View(template);
    }

    // ──────────────────────────────────────────────────────
    // Save layout (AJAX from designer)
    // ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest req)
    {
        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == req.BillTemplateId);
        if (template is null) return NotFound();

        template.LayoutJson = req.LayoutJson;
        template.PaperSize = req.PaperSize;
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ──────────────────────────────────────────────────────
    // Edit metadata
    // ──────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();
        ViewBag.Stores = await _db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(template);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BillTemplate model)
    {
        if (id != model.BillTemplateId) return NotFound();
        if (model.TemplateScope == InvoiceTemplateScope.Store && model.StoreId is null)
            ModelState.AddModelError(nameof(BillTemplate.StoreId), "Store is required when template scope is Store.");
        if (!ModelState.IsValid)
        {
            ViewBag.Stores = await _db.Stores.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
            return View(model);
        }

        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        template.TemplateName = model.TemplateName;
        template.TemplateType = model.TemplateType;
        template.DocumentType = model.DocumentType;
        template.TemplateScope = model.TemplateScope;
        template.StoreId = model.TemplateScope == InvoiceTemplateScope.Store ? model.StoreId : null;
        template.PaperSize = model.PaperSize;
        template.ThemeName = model.ThemeName;
        template.AccentColor = model.AccentColor;
        template.ShowSignature = model.ShowSignature;
        template.ShowStamp = model.ShowStamp;
        template.ShowPartyBalance = model.ShowPartyBalance;
        template.EnableFreeItemQuantity = model.EnableFreeItemQuantity;
        template.ShowItemDescription = model.ShowItemDescription;
        template.ShowPhoneOnInvoice = model.ShowPhoneOnInvoice;
        template.IsDefault = model.IsDefault;
        template.UpdatedAtUtc = DateTime.UtcNow;

        if (template.IsDefault)
            await UnsetOtherDefaults(template);

        await _db.SaveChangesAsync();

        TempData["Ok"] = "Template updated.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────
    // Delete
    // ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        _db.BillTemplates.Remove(template);
        await _db.SaveChangesAsync();

        TempData["Ok"] = "Template deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────
    // Set as default (AJAX)
    // ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(Guid id)
    {
        var template = await _db.BillTemplates
            .FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        await UnsetOtherDefaults(template);
        template.IsDefault = true;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Ok"] = $"'{template.TemplateName}' is now the default.";
        return RedirectToAction(nameof(Index));
    }

    // ──────────────────────────────────────────────────────
    // Company logo upload
    // ──────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadLogo(IFormFile logo)
    {
        if (logo is null || logo.Length == 0)
            return Json(new { success = false, message = "No file selected." });

        if (logo.Length > 2 * 1024 * 1024)
            return Json(new { success = false, message = "File too large (max 2 MB)." });

        var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            return Json(new { success = false, message = "Only PNG, JPG, WEBP allowed." });

        var companyId = GetCompanyId();
        var company = await _db.Companies.FindAsync(companyId);
        if (company is null) return NotFound();

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "logos");
        Directory.CreateDirectory(uploadsDir);

        // Delete old logo if exists
        if (!string.IsNullOrEmpty(company.LogoPath))
        {
            var oldPath = Path.Combine(_env.WebRootPath, company.LogoPath.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var fileName = $"{companyId}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await logo.CopyToAsync(stream);
        }

        company.LogoPath = $"uploads/logos/{fileName}";
        company.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new { success = true, path = company.LogoPath });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSignature(IFormFile signature)
    {
        return await UploadCompanyAssetAsync(signature, "signatures", (company, path) => company.SignaturePath = path);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadStamp(IFormFile stamp)
    {
        return await UploadCompanyAssetAsync(stamp, "stamps", (company, path) => company.StampPath = path);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo()
    {
        return await RemoveCompanyAssetAsync(c => c.LogoPath, (c, p) => c.LogoPath = p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSignature()
    {
        return await RemoveCompanyAssetAsync(c => c.SignaturePath, (c, p) => c.SignaturePath = p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStamp()
    {
        return await RemoveCompanyAssetAsync(c => c.StampPath, (c, p) => c.StampPath = p);
    }

    private async Task<IActionResult> RemoveCompanyAssetAsync(Func<Company, string?> getter, Action<Company, string?> setter)
    {
        try
        {
            var company = await _db.Companies.FirstOrDefaultAsync(c => c.CompanyId == GetCompanyId());
            if (company is null) return Json(new { success = false, message = "Company not found." });

            var oldPath = getter(company);
            if (!string.IsNullOrWhiteSpace(oldPath))
            {
                var fullPath = Path.Combine(_env.WebRootPath, oldPath.TrimStart('/'));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }

            setter(company, null);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Server error." });
        }
    }

    // ──────────────────────────────────────────────────────
    // Preview receipt PDF
    // ──────────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> PreviewReceipt(Guid id)
    {
        var template = await _db.BillTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == GetCompanyId());
        if (company is null) return NotFound();

        // Get latest completed bill for preview
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Item)
            .Include(b => b.Payments)
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .Include(b => b.CashierUser)
            .Where(b => b.Status == 2)
            .OrderByDescending(b => b.BillDate)
            .FirstOrDefaultAsync();

        // If no completed bills, create a sample bill for preview
        if (bill is null)
        {
            var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync()
                ?? new Store { Name = company.Name, Address = company.Address, Phone = company.Phone, GstNo = company.GstNo };

            bill = new PosBill
            {
                BillNo = "PREVIEW-001",
                BillDate = DateTime.Today,
                Store = store,
                SubTotal = 570m,
                TaxTotal = 10.50m,
                DiscountTotal = 0m,
                GrandTotal = 580.50m,
                Status = 2,
                CompletedAtUtc = DateTime.UtcNow,
                Lines = new List<PosBillLine>
                {
                    new() { ItemNameSnapshot = "Basmati Rice 5kg", SkuSnapshot = "ITM-001", BarcodeSnapshot = "8901000000011", Qty = 1, UnitPrice = 220m, MrpSnapshot = 220m, GstPercentSnapshot = 0m, DiscountAmount = 0, LineTotal = 220m },
                    new() { ItemNameSnapshot = "Milk 1L", SkuSnapshot = "ITM-002", BarcodeSnapshot = "8901000000013", Qty = 1, UnitPrice = 210m, MrpSnapshot = 210m, GstPercentSnapshot = 5m, DiscountAmount = 0, LineTotal = 210m },
                    new() { ItemNameSnapshot = "Sugar 1kg", SkuSnapshot = "ITM-003", BarcodeSnapshot = "8901000000012", Qty = 1, UnitPrice = 140m, MrpSnapshot = 140m, GstPercentSnapshot = 0m, DiscountAmount = 0, LineTotal = 140m }
                },
                Payments = new List<Payment>
                {
                    new() { Method = "Cash", Amount = 580.50m, PaidAtUtc = DateTime.UtcNow, IsRefund = false }
                }
            };
        }

        var pdf = _receiptPdf.Generate(bill, template, company);
        return File(pdf, "application/pdf", $"receipt_preview_{template.TemplateName}.pdf");
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Cashier")]
    public async Task<IActionResult> PreviewInvoice(Guid id)
    {
        var template = await _db.BillTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.BillTemplateId == id);
        if (template is null) return NotFound();

        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == GetCompanyId());
        if (company is null) return NotFound();

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .OrderByDescending(i => i.InvoiceDate)
            .FirstOrDefaultAsync();

        if (invoice is null)
        {
            var sampleCustomer = await _db.Customers.AsNoTracking().OrderBy(c => c.Name).FirstOrDefaultAsync();
            var sampleWarehouse = await _db.Warehouses.AsNoTracking().OrderBy(w => w.Name).FirstOrDefaultAsync();

            invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                InvoiceNo = "TAX-2026-0001",
                InvoiceDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                DocumentType = InvoiceDocumentType.TaxInvoice,
                Status = 2,
                TotalAmount = 590m,
                Customer = sampleCustomer ?? new Customer { Name = "Sample Party", Phone = "9000000000", Email = "sample@party.com" },
                Warehouse = sampleWarehouse ?? new Warehouse { Name = "Main Warehouse" },
                Lines = new List<InvoiceLine>
                {
                    new() { ItemNameSnapshot = "Paracetamol 500mg", Qty = 2, UnitPrice = 25m, GstPercentSnapshot = 5m, DiscountAmount = 0m },
                    new() { ItemNameSnapshot = "Vitamin Syrup", Qty = 1, UnitPrice = 180m, GstPercentSnapshot = 12m, DiscountAmount = 0m },
                    new() { ItemNameSnapshot = "Protein Powder", Qty = 1, UnitPrice = 360m, GstPercentSnapshot = 18m, DiscountAmount = 0m }
                }
            };
        }

        var pdf = _invoicePdf.Generate(invoice, template, company);
        return File(pdf, "application/pdf", $"invoice_preview_{template.TemplateName}.pdf");
    }

    // ──────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────
    private async Task UnsetOtherDefaults(BillTemplate source)
    {
        var others = await _db.BillTemplates
            .Where(t =>
                t.TemplateType == source.TemplateType &&
                t.DocumentType == source.DocumentType &&
                t.TemplateScope == source.TemplateScope &&
                t.StoreId == source.StoreId &&
                t.IsDefault &&
                t.BillTemplateId != source.BillTemplateId)
            .ToListAsync();
        foreach (var t in others) t.IsDefault = false;
        if (others.Count > 0) await _db.SaveChangesAsync();
    }

    private async Task<IActionResult> UploadCompanyAssetAsync(
        IFormFile? file,
        string folderName,
        Action<Company, string> setCompanyPath)
    {
        if (file is null || file.Length == 0)
            return Json(new { success = false, message = "No file selected." });

        if (file.Length > 2 * 1024 * 1024)
            return Json(new { success = false, message = "File too large (max 2 MB)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            return Json(new { success = false, message = "Only PNG, JPG, WEBP allowed." });

        var companyId = GetCompanyId();
        var company = await _db.Companies.FindAsync(companyId);
        if (company is null) return NotFound();

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", folderName);
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{companyId}{ext}";
        var relativePath = $"uploads/{folderName}/{fileName}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        setCompanyPath(company, relativePath);
        company.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new { success = true, path = relativePath });
    }

    private static string GetPresetLayoutJson(string preset, byte templateType)
    {
        var key = (preset ?? "modern").Trim().ToLowerInvariant();
        if (key == "classic")
            return GetClassicLayoutJson(templateType);
        return GetModernLayoutJson(templateType);
    }

    private static string GetModernLayoutJson(byte templateType)
    {
        var components = new object[]
        {
            new { type = "logo",         props = new { maxHeight = 50, align = "center", marginTop = 0, marginBottom = 2 } },
            new { type = "header_row",   props = new { leftText = "", centerMode = "logo", centerText = "", rightText = "", fontSize = 9, logoHeight = 28, showSeparator = false, marginTop = 0, marginBottom = 2 } },
            new { type = "store_header", props = new { showAddress = true, showPhone = true, showGst = true, showTitle = true, showSeparator = true, headerText = "", marginTop = 0, marginBottom = 2 } },
            new { type = "social_row",   props = new { instagram = "", whatsapp = "", facebook = "", xhandle = "", youtube = "", phone = "", email = "", website = "", align = "center", separator = " | ", fontSize = 9, marginTop = 0, marginBottom = 2 } },
            new { type = "bill_info",    props = new { showInvoiceNo = true, showInvoiceDate = true, showDocumentType = true, showDueDate = true, showReferenceInvoice = true, showCustomerName = true, showCustomerPhone = true, showCustomerEmail = false, showWarehouse = true, showCashier = false, showSeparator = true, marginTop = 0, marginBottom = 2 } },
            new { type = "items_table",  props = new { showSerial = true, showItem = true, showSku = false, showHsn = false, showQty = true, showRate = true, showDiscountColumn = false, showTaxPercent = true, showTaxAmount = false, showAmount = true, labelSerial = "#", labelItem = "Item", labelSku = "SKU", labelHsn = "HSN", labelQty = "Qty", labelRate = "Rate", labelDiscount = "Disc", labelTaxPercent = "Tax%", labelTaxAmount = "Tax Amt", labelAmount = "Amt", widthSerial = 20, widthItem = 4, widthSku = 36, widthHsn = 40, widthQty = 30, widthRate = 55, widthDiscount = 48, widthTaxPercent = 45, widthTaxAmount = 52, widthAmount = 65, showHeaderBackground = true, headerBgColor = "#f2f2f2", headerTextColor = "#111111", showGrid = false, gridColor = "#d9d9d9", zebraRows = false, zebraColor = "#fafafa", showDescriptionRow = true, showBottomSeparator = true, marginTop = 0, marginBottom = 2 } },
            new { type = "totals",       props = new { showDiscount = true, marginTop = 0, marginBottom = 2 } },
            new { type = "payments",     props = new { marginTop = 0, marginBottom = 2 } },
            new { type = "tax_summary",  props = new { marginTop = 0, marginBottom = 2 } },
            new { type = "footer",       props = new { text = "Thank you for shopping!", showItemCount = true, showTotalInWords = true, showComputerGenerated = true, marginTop = 0, marginBottom = 2 } }
        };
        return System.Text.Json.JsonSerializer.Serialize(components);
    }

    private static string GetClassicLayoutJson(byte templateType)
    {
        // Classic style similar to traditional retail invoices:
        // centered branding, compact bill header, item grid, summary and signature/terms blocks.
        var components = new object[]
        {
            new { type = "logo",         props = new { maxHeight = 52, align = "center", marginTop = 0, marginBottom = 2 } },
            new { type = "header_row",   props = new { leftText = "", centerMode = "logo", centerText = "", rightText = "", fontSize = 9, logoHeight = 30, showSeparator = true, marginTop = 0, marginBottom = 2 } },
            new { type = "store_header", props = new { showAddress = true, showPhone = true, showGst = true, showTitle = true, showSeparator = true, headerText = templateType == 1 ? "RETAIL INVOICE" : "TAX INVOICE", marginTop = 0, marginBottom = 2 } },
            new { type = "social_row",   props = new { instagram = "", whatsapp = "", facebook = "", xhandle = "", youtube = "", phone = "", email = "", website = "", align = "center", separator = " | ", fontSize = 9, marginTop = 0, marginBottom = 2 } },
            new { type = "bill_info",    props = new { showInvoiceNo = true, showInvoiceDate = true, showDocumentType = true, showDueDate = true, showReferenceInvoice = true, showCustomerName = true, showCustomerPhone = true, showCustomerEmail = false, showWarehouse = true, showCashier = false, showSeparator = true, marginTop = 0, marginBottom = 2 } },
            new { type = "separator",    props = new { style = "solid", thickness = 1, color = "#555555", marginTop = 0, marginBottom = 2 } },
            new { type = "items_table",  props = new { showSerial = true, showItem = true, showSku = false, showHsn = true, showQty = true, showRate = true, showDiscountColumn = false, showTaxPercent = true, showTaxAmount = false, showAmount = true, labelSerial = "#", labelItem = "Item", labelSku = "SKU", labelHsn = "HSN", labelQty = "Qty", labelRate = "Rate", labelDiscount = "Disc", labelTaxPercent = "Tax%", labelTaxAmount = "Tax Amt", labelAmount = "Amt", widthSerial = 20, widthItem = 4, widthSku = 36, widthHsn = 40, widthQty = 30, widthRate = 55, widthDiscount = 48, widthTaxPercent = 45, widthTaxAmount = 52, widthAmount = 65, showHeaderBackground = true, headerBgColor = "#f2f2f2", headerTextColor = "#111111", showGrid = true, gridColor = "#d9d9d9", zebraRows = true, zebraColor = "#fafafa", showDescriptionRow = true, showBottomSeparator = true, marginTop = 0, marginBottom = 2 } },
            new { type = "separator",    props = new { style = "solid", thickness = 1, color = "#555555", marginTop = 0, marginBottom = 2 } },
            new { type = "totals",       props = new { showDiscount = true, marginTop = 0, marginBottom = 2 } },
            new { type = "payments",     props = new { marginTop = 0, marginBottom = 2 } },
            new { type = "text_block",   props = new { text = "Net Amount: {{grand_total}}", fontSize = 12, fontFamily = "serif", align = "right", bold = true, italic = false, color = "#111111", marginTop = 0, marginBottom = 2 } },
            new { type = "text_block",   props = new { text = "In Words: {{grand_total_words}}", fontSize = 10, fontFamily = "serif", align = "left", bold = false, italic = false, color = "#333333", marginTop = 0, marginBottom = 2 } },
            new { type = "separator",    props = new { style = "dashed", thickness = 1, color = "#999999", marginTop = 0, marginBottom = 2 } },
            new { type = "text_block",   props = new { text = "Terms & Conditions:\n1. Goods once sold will not be taken back.\n2. Keep this invoice for exchange/warranty as per policy.", fontSize = 9, fontFamily = "sans-serif", align = "left", bold = false, italic = false, color = "#333333", marginTop = 0, marginBottom = 2 } },
            new { type = "spacer",       props = new { height = 8, marginTop = 0, marginBottom = 0 } },
            new { type = "text_block",   props = new { text = "Receiver Signature                                Authorised Signatory", fontSize = 9, fontFamily = "sans-serif", align = "left", bold = false, italic = false, color = "#444444", marginTop = 0, marginBottom = 2 } },
            new { type = "footer",       props = new { text = "Thank you for your business!", showItemCount = true, showTotalInWords = true, showComputerGenerated = true, marginTop = 0, marginBottom = 2 } }
        };
        return System.Text.Json.JsonSerializer.Serialize(components);
    }

    public class SaveLayoutRequest
    {
        public Guid BillTemplateId { get; set; }
        public string LayoutJson { get; set; } = "[]";
        public string PaperSize { get; set; } = "Thermal80mm";
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class BarcodeLabelsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BarcodeLabelService _labels;

    public BarcodeLabelsController(ApplicationDbContext db, BarcodeLabelService labels)
    {
        _db = db;
        _labels = labels;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, Guid? categoryId)
    {
        var query = _db.Items.AsNoTracking().Include(i => i.Category)
            .Where(i => i.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(term)
                || i.SKU.ToLower().Contains(term)
                || (i.Barcode != null && i.Barcode.Contains(term)));
        }
        if (categoryId.HasValue)
            query = query.Where(i => i.CategoryId == categoryId);

        var items = await query.OrderBy(i => i.Name).Take(100).ToListAsync();

        ViewBag.Q = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GeneratePdf(List<Guid> selectedItems, string paperSize = "Thermal",
        float labelWidth = 50, float labelHeight = 30, int columns = 3, int fontSize = 8,
        bool showName = true, bool showSku = true, bool showBarcode = true,
        bool showQrCode = false, bool showPrice = true, bool showExpiry = false, int copies = 1)
    {
        if (selectedItems.Count == 0)
        {
            TempData["Error"] = "Select at least one item.";
            return RedirectToAction(nameof(Index));
        }

        var items = await _db.Items.AsNoTracking()
            .Where(i => selectedItems.Contains(i.ItemId))
            .ToListAsync();

        var labelItems = new List<BarcodeLabelService.LabelItem>();
        foreach (var item in items)
        {
            for (int c = 0; c < Math.Max(1, copies); c++)
            {
                labelItems.Add(new BarcodeLabelService.LabelItem
                {
                    Name = item.Name,
                    SKU = item.SKU,
                    Barcode = item.Barcode ?? item.SKU,
                    UnitPrice = item.UnitPrice,
                    MRP = item.MRP
                });
            }
        }

        var options = new BarcodeLabelService.LabelOptions
        {
            PaperSize = paperSize,
            LabelWidthMm = labelWidth,
            LabelHeightMm = labelHeight,
            Columns = columns,
            FontSize = fontSize,
            ShowName = showName,
            ShowSku = showSku,
            ShowBarcode = showBarcode,
            ShowQrCode = showQrCode,
            ShowPrice = showPrice,
            ShowExpiry = showExpiry
        };

        var pdf = _labels.GenerateLabels(labelItems, options);
        return File(pdf, "application/pdf", $"Labels_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    [HttpGet]
    public IActionResult QrCode(string data, int size = 10)
    {
        if (string.IsNullOrWhiteSpace(data))
            return BadRequest();

        var png = _labels.GenerateQrPng(data, size);
        return File(png, "image/png");
    }
}

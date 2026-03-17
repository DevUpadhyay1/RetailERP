using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Controllers;

[Authorize]
public sealed class SearchController : Controller
{
    private readonly ApplicationDbContext _db;

    public SearchController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        q = (q ?? string.Empty).Trim();

        var vm = new SearchVm { Query = q };
        if (string.IsNullOrWhiteSpace(q))
            return View(vm);

        vm.Items = await _db.Items
            .AsNoTracking()
            .Where(x => x.SKU.Contains(q) || x.Name.Contains(q) || (x.Barcode != null && x.Barcode.Contains(q)))
            .OrderBy(x => x.SKU)
            .Take(10)
            .Select(x => new ResultRow
            {
                Title = x.SKU + " - " + x.Name,
                Subtitle = x.Barcode,
                Url = Url.Action("Details", "Items", new { id = x.ItemId })!
            })
            .ToListAsync();

        vm.Customers = await _db.Customers
            .AsNoTracking()
            .Where(x => x.Name.Contains(q) || (x.Phone != null && x.Phone.Contains(q)) || (x.Email != null && x.Email.Contains(q)))
            .OrderBy(x => x.Name)
            .Take(10)
            .Select(x => new ResultRow
            {
                Title = x.Name,
                Subtitle = x.Phone ?? x.Email,
                Url = Url.Action("Details", "Customers", new { id = x.CustomerId })!
            })
            .ToListAsync();

        vm.Suppliers = await _db.Suppliers
            .AsNoTracking()
            .Where(x => x.Name.Contains(q) || (x.Phone != null && x.Phone.Contains(q)) || (x.Email != null && x.Email.Contains(q)))
            .OrderBy(x => x.Name)
            .Take(10)
            .Select(x => new ResultRow
            {
                Title = x.Name,
                Subtitle = x.Phone ?? x.Email,
                Url = Url.Action("Details", "Suppliers", new { id = x.SupplierId })!
            })
            .ToListAsync();

        vm.Invoices = await _db.Invoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.InvoiceNo.Contains(q) || (x.Customer != null && x.Customer.Name.Contains(q)))
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.InvoiceNo)
            .Take(10)
            .Select(x => new ResultRow
            {
                Title = x.InvoiceNo + " • " + (x.Customer != null ? x.Customer.Name : "-") + " • " + x.TotalAmount.ToString("0.00"),
                Subtitle = x.InvoiceDate.ToString("yyyy-MM-dd"),
                Url = Url.Action("Edit", "Invoices", new { id = x.InvoiceId })!
            })
            .ToListAsync();

        vm.Purchases = await _db.Purchases
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Where(x => x.PurchaseNo.Contains(q) || (x.Supplier != null && x.Supplier.Name.Contains(q)))
            .OrderByDescending(x => x.PurchaseDate)
            .ThenByDescending(x => x.PurchaseNo)
            .Take(10)
            .Select(x => new ResultRow
            {
                Title = x.PurchaseNo + " • " + (x.Supplier != null ? x.Supplier.Name : "-") + " • " + x.TotalAmount.ToString("0.00"),
                Subtitle = x.PurchaseDate.ToString("yyyy-MM-dd"),
                Url = Url.Action("Edit", "Purchases", new { id = x.PurchaseId })!
            })
            .ToListAsync();

        return View(vm);
    }

    public sealed class SearchVm
    {
        public string Query { get; set; } = string.Empty;

        public List<ResultRow> Invoices { get; set; } = new();
        public List<ResultRow> Purchases { get; set; } = new();
        public List<ResultRow> Items { get; set; } = new();
        public List<ResultRow> Customers { get; set; } = new();
        public List<ResultRow> Suppliers { get; set; } = new();

        public int TotalCount => Invoices.Count + Purchases.Count + Items.Count + Customers.Count + Suppliers.Count;
    }

    public sealed class ResultRow
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}

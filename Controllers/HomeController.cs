using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models;

namespace RetailERP.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

    // Entry: anonymous -> Landing, logged-in -> Dashboard
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Dashboard));

        return RedirectToAction(nameof(Landing));
    }

    // Public landing page (before login)
    [AllowAnonymous]
    public IActionResult Landing()
    {
        ViewData["Title"] = "Welcome";
        return View();
    }

    // Real dashboard (after login)
    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        ViewData["Title"] = "Dashboard";

        var fromUtc = DateTime.UtcNow.Date.AddDays(-7);

        var vm = new DashboardVm
        {
            ItemsCount = await _db.Items.AsNoTracking().CountAsync(),
            CustomersCount = await _db.Customers.AsNoTracking().CountAsync(),
            EmployeesCount = await _db.Employees.AsNoTracking().CountAsync(),
            WarehousesCount = await _db.Warehouses.AsNoTracking().CountAsync(),

            // assumes: Draft=1, Posted=2 (matches your InvoiceService)
            DraftInvoicesCount = await _db.Invoices.AsNoTracking().CountAsync(x => x.Status == 1),
            PostedLast7DaysCount = await _db.Invoices.AsNoTracking()
                .CountAsync(x => x.Status == 2 && x.PostedAt != null && x.PostedAt >= fromUtc),

            SalesLast7Days = await _db.Invoices.AsNoTracking()
                .Where(x => x.Status == 2 && x.PostedAt != null && x.PostedAt >= fromUtc)
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m,

            RecentInvoices = await _db.Invoices.AsNoTracking()
                .Include(x => x.Customer)
                .Include(x => x.Warehouse)
                .OrderByDescending(x => x.InvoiceDate)
                .ThenByDescending(x => x.InvoiceNo)
                .Take(8)
                .Select(x => new RecentInvoiceRow
                {
                    InvoiceId = x.InvoiceId,
                    InvoiceNo = x.InvoiceNo,
                    InvoiceDate = x.InvoiceDate,
                    CustomerName = x.Customer.Name,
                    WarehouseName = x.Warehouse != null ? x.Warehouse.Name : "-",
                    TotalAmount = x.TotalAmount,
                    Status = x.Status
                })
                .ToListAsync(),

            LowStock = await _db.Stocks.AsNoTracking()
                .Include(s => s.Item)
                .Include(s => s.Warehouse)
                .Where(s => s.Quantity <= (decimal)s.Item.ReorderLevel)
                .OrderBy(s => s.Quantity)
                .Take(10)
                .Select(s => new LowStockRow
                {
                    ItemSku = s.Item.SKU,
                    ItemName = s.Item.Name,
                    WarehouseName = s.Warehouse.Name,
                    Quantity = s.Quantity,
                    ReorderLevel = s.Item.ReorderLevel
                })
                .ToListAsync()
        };

        return View(vm);
    }
}
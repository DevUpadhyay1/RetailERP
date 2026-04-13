using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Cashier,Finance,Inventory")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Sales(DateTime? from, DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-7)).Date;
        var toDateExclusive = ((to ?? DateTime.Today).Date).AddDays(1);

        var posted = _db.Invoices
            .AsNoTracking()
            .Where(x => x.Status == 2 && x.PostedAt != null)
            .Where(x => x.PostedAt >= fromDate && x.PostedAt < toDateExclusive);

        var rows = await posted
            .OrderByDescending(x => x.PostedAt)
            .Take(500)
            .Select(x => new SalesRowVm
            {
                InvoiceId = x.InvoiceId,
                InvoiceNo = x.InvoiceNo,
                PostedAt = x.PostedAt!.Value,
                TotalAmount = x.TotalAmount
            })
            .ToListAsync();

        var vm = new SalesReportVm
        {
            From = fromDate,
            To = toDateExclusive.AddDays(-1),
            TotalInvoices = rows.Count,
            TotalSales = rows.Sum(x => x.TotalAmount),
            Rows = rows
        };

        return View(vm);
    }

    public sealed class SalesReportVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalSales { get; set; }
        public List<SalesRowVm> Rows { get; set; } = new();

        // Backward-compatible aliases used by the existing Razor view.
        public decimal TotalInvoiceSales => TotalSales;
        public decimal TotalPosSales => 0m;
        public int TotalPosBills => 0;
    }

    public sealed class SalesRowVm
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNo { get; set; } = "";
        public DateTime PostedAt { get; set; }
        public decimal TotalAmount { get; set; }

        // Backward-compatible aliases used by the existing Razor view.
        public string RefNo => InvoiceNo;
        public string Source => "Invoice";
        public DateTime Date => PostedAt;
    }
}
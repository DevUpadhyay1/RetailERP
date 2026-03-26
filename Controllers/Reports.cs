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

        // ── Invoice Sales ──
        var postedInvoices = _db.Invoices
            .AsNoTracking()
            .Where(x => x.Status == 2 && x.PostedAt != null)
            .Where(x => x.PostedAt >= fromDate && x.PostedAt < toDateExclusive);

        var totalInvoices = await postedInvoices.CountAsync();
        var totalInvoiceSales = await postedInvoices.SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var invoiceRows = await postedInvoices
            .OrderByDescending(x => x.PostedAt)
            .Take(500)
            .Select(x => new SalesRowVm
            {
                Id = x.InvoiceId,
                RefNo = x.InvoiceNo,
                Date = x.PostedAt!.Value,
                TotalAmount = x.TotalAmount,
                Source = "Invoice"
            })
            .ToListAsync();

        // ── POS Sales ──
        var completedPos = _db.PosBills
            .AsNoTracking()
            .Where(x => x.Status == 2 && x.CompletedAtUtc != null)
            .Where(x => x.BillDate >= fromDate && x.BillDate < toDateExclusive);

        var totalPosBills = await completedPos.CountAsync();
        var totalPosSales = await completedPos.SumAsync(x => (decimal?)x.GrandTotal) ?? 0m;

        var posRows = await completedPos
            .OrderByDescending(x => x.CompletedAtUtc)
            .Take(500)
            .Select(x => new SalesRowVm
            {
                Id = x.PosBillId,
                RefNo = x.BillNo,
                Date = x.CompletedAtUtc!.Value,
                TotalAmount = x.GrandTotal,
                Source = "POS"
            })
            .ToListAsync();

        // ── Merge & sort ──
        var allRows = invoiceRows.Concat(posRows)
            .OrderByDescending(r => r.Date)
            .Take(500)
            .ToList();

        var vm = new SalesReportVm
        {
            From = fromDate,
            To = toDateExclusive.AddDays(-1),
            TotalInvoices = totalInvoices,
            TotalPosBills = totalPosBills,
            TotalInvoiceSales = totalInvoiceSales,
            TotalPosSales = totalPosSales,
            TotalSales = totalInvoiceSales + totalPosSales,
            Rows = allRows
        };

        return View(vm);
    }

    public sealed class SalesReportVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int TotalInvoices { get; set; }
        public int TotalPosBills { get; set; }
        public decimal TotalInvoiceSales { get; set; }
        public decimal TotalPosSales { get; set; }
        public decimal TotalSales { get; set; }
        public List<SalesRowVm> Rows { get; set; } = new();
    }

    public sealed class SalesRowVm
    {
        public Guid Id { get; set; }
        public string RefNo { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public string Source { get; set; } = "";
    }
}
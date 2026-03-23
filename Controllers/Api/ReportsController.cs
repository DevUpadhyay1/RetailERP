using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Models.Api;

namespace RetailERP.Controllers.Api;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Manager,Finance")]
public class ReportsController : ApiBaseController
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Sales report: daily sales totals for a date range, plus overall summary.
    /// Defaults to last 30 days if no dates provided.
    /// </summary>
    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to,
                                            [FromQuery] Guid? storeId)
    {
        var start = from ?? DateTime.Today.AddDays(-30);
        var end = to ?? DateTime.Today;

        var q = _db.PosBills.AsNoTracking()
                    .Where(b => b.Status == 2 && b.BillDate >= start && b.BillDate <= end);

        if (storeId.HasValue) q = q.Where(b => b.StoreId == storeId.Value);

        var dailyData = await q
            .GroupBy(b => b.BillDate)
            .Select(g => new DailySalesDto
            {
                Date = g.Key,
                BillCount = g.Count(),
                Revenue = g.Sum(x => x.GrandTotal)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // Get aggregated tax & discount
        var agg = await _db.PosBills.AsNoTracking()
            .Where(b => b.Status == 2 && b.BillDate >= start && b.BillDate <= end)
            .GroupBy(_ => 1)
            .Select(g => new { Tax = g.Sum(x => x.TaxTotal), Disc = g.Sum(x => x.DiscountTotal) })
            .FirstOrDefaultAsync();

        var report = new SalesReportDto
        {
            From = start,
            To = end,
            TotalBills = dailyData.Sum(d => d.BillCount),
            TotalRevenue = dailyData.Sum(d => d.Revenue),
            TotalTax = agg?.Tax ?? 0,
            TotalDiscount = agg?.Disc ?? 0,
            Daily = dailyData
        };

        return Ok(ApiResponse<SalesReportDto>.Ok(report));
    }
}

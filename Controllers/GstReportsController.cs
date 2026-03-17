using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Finance")]
public class GstReportsController : Controller
{
    private readonly GstReportService _gst;

    public GstReportsController(GstReportService gst) => _gst = gst;

    // ═══════════════════════════════════════════════════════════
    // GSTR-1
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Gstr1(DateTime? from, DateTime? to)
    {
        var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        var b2b = await _gst.GetB2BAsync(start, end);
        var b2cs = await _gst.GetB2CSAsync(start, end);
        var hsn = await _gst.GetHsnSummaryAsync(start, end);

        return View(new Gstr1ViewModel
        {
            From = start,
            To = end,
            B2B = b2b,
            B2CS = b2cs,
            Hsn = hsn
        });
    }

    // ═══════════════════════════════════════════════════════════
    // GSTR-3B
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Gstr3B(DateTime? from, DateTime? to)
    {
        var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        var data = await _gst.GetGstr3BAsync(start, end);
        return View(data);
    }

    // ═══════════════════════════════════════════════════════════
    // HSN Summary (standalone)
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> HsnSummary(DateTime? from, DateTime? to)
    {
        var start = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        var rows = await _gst.GetHsnSummaryAsync(start, end);
        ViewBag.From = start;
        ViewBag.To = end;
        return View(rows);
    }
}

public sealed class Gstr1ViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<Gstr1B2BRow> B2B { get; set; } = new();
    public List<Gstr1B2CSRow> B2CS { get; set; } = new();
    public List<HsnSummaryRow> Hsn { get; set; } = new();
}

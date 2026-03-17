using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Services;

/// <summary>
/// Sprint 8: Generates GSTR-1, GSTR-3B and HSN summary data
/// from completed POS bills and posted invoices.
/// </summary>
public class GstReportService
{
    private readonly ApplicationDbContext _db;

    public GstReportService(ApplicationDbContext db) => _db = db;

    // ═══════════════════════════════════════════════════════════
    // GSTR-1  —  Outward supplies
    // ═══════════════════════════════════════════════════════════

    /// <summary>B2B invoices where customer has GSTIN (table 4A).</summary>
    public async Task<List<Gstr1B2BRow>> GetB2BAsync(DateTime from, DateTime to)
    {
        var bills = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Lines)
            .Where(b => b.Status == 2 && b.BillDate >= from && b.BillDate <= to)
            .Where(b => b.Customer != null && b.Customer.Gstin != null && b.Customer.Gstin != "")
            .ToListAsync();

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .Where(i => i.Status == 2 && i.PostedAt != null)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .Where(i => i.Customer != null && i.Customer.Gstin != null && i.Customer.Gstin != "")
            .ToListAsync();

        var rows = new List<Gstr1B2BRow>();

        foreach (var b in bills)
        {
            var taxable = b.SubTotal - b.DiscountTotal;
            rows.Add(new Gstr1B2BRow
            {
                Gstin = b.Customer!.Gstin!,
                CustomerName = b.Customer.Name,
                InvoiceNo = b.BillNo,
                InvoiceDate = b.BillDate,
                InvoiceType = b.InvoiceType == "Tax" ? "B2B" : "B2B",
                TaxableValue = taxable,
                TaxRate = b.Lines.Count > 0
                    ? b.Lines.Where(l => l.GstPercentSnapshot.HasValue).Select(l => l.GstPercentSnapshot!.Value).FirstOrDefault()
                    : 0,
                CgstAmount = b.TaxTotal / 2,
                SgstAmount = b.TaxTotal / 2,
                IgstAmount = 0, // Inter-state logic can be added later
                TotalValue = b.GrandTotal
            });
        }

        foreach (var inv in invoices)
        {
            var lineTotal = inv.Lines.Sum(l => l.Qty * l.UnitPrice - l.DiscountAmount);
            var taxTotal = inv.Lines.Sum(l =>
                l.GstPercentSnapshot.HasValue
                    ? (l.Qty * l.UnitPrice - l.DiscountAmount) * l.GstPercentSnapshot.Value / 100m
                    : 0);

            rows.Add(new Gstr1B2BRow
            {
                Gstin = inv.Customer!.Gstin!,
                CustomerName = inv.Customer.Name,
                InvoiceNo = inv.InvoiceNo,
                InvoiceDate = inv.InvoiceDate,
                InvoiceType = "B2B",
                TaxableValue = lineTotal,
                TaxRate = inv.Lines.Count > 0
                    ? inv.Lines.Where(l => l.GstPercentSnapshot.HasValue).Select(l => l.GstPercentSnapshot!.Value).FirstOrDefault()
                    : 0,
                CgstAmount = taxTotal / 2,
                SgstAmount = taxTotal / 2,
                IgstAmount = 0,
                TotalValue = lineTotal + taxTotal
            });
        }

        return rows.OrderBy(r => r.InvoiceDate).ToList();
    }

    /// <summary>B2C Small — aggregate of invoices without GSTIN (table 7).</summary>
    public async Task<List<Gstr1B2CSRow>> GetB2CSAsync(DateTime from, DateTime to)
    {
        // POS bills without customer GSTIN, grouped by tax rate
        var billLines = await _db.PosBillLines
            .AsNoTracking()
            .Include(l => l.PosBill).ThenInclude(b => b!.Customer)
            .Where(l => l.PosBill!.Status == 2 && l.PosBill.BillDate >= from && l.PosBill.BillDate <= to)
            .Where(l => l.PosBill!.Customer == null || l.PosBill.Customer.Gstin == null || l.PosBill.Customer.Gstin == "")
            .ToListAsync();

        var invoiceLines = await _db.InvoiceLines
            .AsNoTracking()
            .Include(l => l.Invoice).ThenInclude(i => i!.Customer)
            .Where(l => l.Invoice!.Status == 2 && l.Invoice.PostedAt != null)
            .Where(l => l.Invoice!.InvoiceDate >= from && l.Invoice.InvoiceDate <= to)
            .Where(l => l.Invoice!.Customer == null || l.Invoice.Customer!.Gstin == null || l.Invoice.Customer.Gstin == "")
            .ToListAsync();

        var grouped = new Dictionary<decimal, (decimal Taxable, decimal Tax)>();

        foreach (var l in billLines)
        {
            var rate = l.GstPercentSnapshot ?? 0;
            var taxable = l.LineTotal;
            var tax = rate > 0 ? taxable * rate / 100m : 0;
            if (!grouped.ContainsKey(rate)) grouped[rate] = (0, 0);
            grouped[rate] = (grouped[rate].Taxable + taxable, grouped[rate].Tax + tax);
        }

        foreach (var l in invoiceLines)
        {
            var rate = l.GstPercentSnapshot ?? 0;
            var taxable = l.Qty * l.UnitPrice - l.DiscountAmount;
            var tax = rate > 0 ? taxable * rate / 100m : 0;
            if (!grouped.ContainsKey(rate)) grouped[rate] = (0, 0);
            grouped[rate] = (grouped[rate].Taxable + taxable, grouped[rate].Tax + tax);
        }

        return grouped
            .OrderBy(g => g.Key)
            .Select(g => new Gstr1B2CSRow
            {
                TaxRate = g.Key,
                TaxableValue = Math.Round(g.Value.Taxable, 2),
                CgstAmount = Math.Round(g.Value.Tax / 2, 2),
                SgstAmount = Math.Round(g.Value.Tax / 2, 2),
                IgstAmount = 0
            })
            .ToList();
    }

    /// <summary>HSN-wise summary of outward supplies (table 12).</summary>
    public async Task<List<HsnSummaryRow>> GetHsnSummaryAsync(DateTime from, DateTime to)
    {
        var billLines = await _db.PosBillLines
            .AsNoTracking()
            .Where(l => l.PosBill!.Status == 2 && l.PosBill.BillDate >= from && l.PosBill.BillDate <= to)
            .ToListAsync();

        var invoiceLines = await _db.InvoiceLines
            .AsNoTracking()
            .Where(l => l.Invoice!.Status == 2 && l.Invoice.PostedAt != null)
            .Where(l => l.Invoice!.InvoiceDate >= from && l.Invoice.InvoiceDate <= to)
            .ToListAsync();

        var grouped = new Dictionary<string, HsnAccumulator>();

        foreach (var l in billLines)
        {
            var hsn = l.HsnCodeSnapshot ?? "NA";
            if (!grouped.ContainsKey(hsn)) grouped[hsn] = new();
            var a = grouped[hsn];
            a.Qty += l.Qty;
            a.TaxableValue += l.LineTotal;
            var tax = l.GstPercentSnapshot.HasValue ? l.LineTotal * l.GstPercentSnapshot.Value / 100m : 0;
            a.TaxAmount += tax;
            a.GstRate = l.GstPercentSnapshot ?? 0;
        }

        foreach (var l in invoiceLines)
        {
            var hsn = l.HsnCodeSnapshot ?? "NA";
            if (!grouped.ContainsKey(hsn)) grouped[hsn] = new();
            var a = grouped[hsn];
            a.Qty += l.Qty;
            var taxable = l.Qty * l.UnitPrice - l.DiscountAmount;
            a.TaxableValue += taxable;
            var tax = l.GstPercentSnapshot.HasValue ? taxable * l.GstPercentSnapshot.Value / 100m : 0;
            a.TaxAmount += tax;
            a.GstRate = l.GstPercentSnapshot ?? 0;
        }

        return grouped
            .OrderBy(g => g.Key)
            .Select(g => new HsnSummaryRow
            {
                HsnCode = g.Key,
                TotalQty = Math.Round(g.Value.Qty, 2),
                TaxableValue = Math.Round(g.Value.TaxableValue, 2),
                GstRate = g.Value.GstRate,
                CgstAmount = Math.Round(g.Value.TaxAmount / 2, 2),
                SgstAmount = Math.Round(g.Value.TaxAmount / 2, 2),
                IgstAmount = 0,
                TotalTax = Math.Round(g.Value.TaxAmount, 2)
            })
            .ToList();
    }

    // ═══════════════════════════════════════════════════════════
    // GSTR-3B  —  Monthly summary return
    // ═══════════════════════════════════════════════════════════

    public async Task<Gstr3BSummary> GetGstr3BAsync(DateTime from, DateTime to)
    {
        // Outward supplies from POS bills
        var posTotals = await _db.PosBills
            .AsNoTracking()
            .Where(b => b.Status == 2 && b.BillDate >= from && b.BillDate <= to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Taxable = g.Sum(b => b.SubTotal - b.DiscountTotal),
                Tax = g.Sum(b => b.TaxTotal),
                Total = g.Sum(b => b.GrandTotal),
                Count = g.Count()
            })
            .FirstOrDefaultAsync();

        // Outward supplies from invoices
        var invLines = await _db.InvoiceLines
            .AsNoTracking()
            .Where(l => l.Invoice!.Status == 2 && l.Invoice.PostedAt != null)
            .Where(l => l.Invoice!.InvoiceDate >= from && l.Invoice.InvoiceDate <= to)
            .ToListAsync();

        var invTaxable = invLines.Sum(l => l.Qty * l.UnitPrice - l.DiscountAmount);
        var invTax = invLines.Sum(l =>
            l.GstPercentSnapshot.HasValue
                ? (l.Qty * l.UnitPrice - l.DiscountAmount) * l.GstPercentSnapshot.Value / 100m
                : 0);

        // Purchase (Input Tax Credit)
        var purchaseLines = await _db.PurchaseLines
            .AsNoTracking()
            .Include(l => l.Item)
            .Where(l => l.Purchase!.Status == 2 && l.Purchase.ReceivedAt != null)
            .Where(l => l.Purchase!.PurchaseDate >= from && l.Purchase.PurchaseDate <= to)
            .ToListAsync();

        var itcAmount = purchaseLines.Sum(l =>
            l.Item?.GstPercent != null
                ? l.Qty * l.UnitCost * l.Item.GstPercent.Value / 100m
                : 0);

        var totalTaxable = (posTotals?.Taxable ?? 0) + invTaxable;
        var totalTax = (posTotals?.Tax ?? 0) + invTax;

        return new Gstr3BSummary
        {
            // 3.1 – Outward supplies
            TotalOutwardTaxable = Math.Round(totalTaxable, 2),
            TotalOutwardTax = Math.Round(totalTax, 2),
            CgstPayable = Math.Round(totalTax / 2, 2),
            SgstPayable = Math.Round(totalTax / 2, 2),
            IgstPayable = 0,

            // 4 – Eligible ITC
            ItcCgst = Math.Round(itcAmount / 2, 2),
            ItcSgst = Math.Round(itcAmount / 2, 2),
            ItcIgst = 0,

            // Net tax payable
            NetCgst = Math.Round(totalTax / 2 - itcAmount / 2, 2),
            NetSgst = Math.Round(totalTax / 2 - itcAmount / 2, 2),
            NetIgst = 0,

            BillCount = (posTotals?.Count ?? 0),
            InvoiceCount = invLines.Select(l => l.InvoiceId).Distinct().Count(),
            PurchaseCount = purchaseLines.Select(l => l.PurchaseId).Distinct().Count(),
            From = from,
            To = to
        };
    }

    // ═══════════════════════════════════════════════════════════
    // DTOs
    // ═══════════════════════════════════════════════════════════

    private class HsnAccumulator
    {
        public decimal Qty;
        public decimal TaxableValue;
        public decimal TaxAmount;
        public decimal GstRate;
    }
}

public sealed class Gstr1B2BRow
{
    public string Gstin { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string InvoiceType { get; set; } = "B2B";
    public decimal TaxableValue { get; set; }
    public decimal TaxRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalValue { get; set; }
}

public sealed class Gstr1B2CSRow
{
    public decimal TaxRate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
}

public sealed class HsnSummaryRow
{
    public string HsnCode { get; set; } = "";
    public decimal TotalQty { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalTax { get; set; }
}

public sealed class Gstr3BSummary
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    // 3.1 – Outward supplies
    public decimal TotalOutwardTaxable { get; set; }
    public decimal TotalOutwardTax { get; set; }
    public decimal CgstPayable { get; set; }
    public decimal SgstPayable { get; set; }
    public decimal IgstPayable { get; set; }

    // 4 – Eligible ITC
    public decimal ItcCgst { get; set; }
    public decimal ItcSgst { get; set; }
    public decimal ItcIgst { get; set; }

    // Net payable
    public decimal NetCgst { get; set; }
    public decimal NetSgst { get; set; }
    public decimal NetIgst { get; set; }

    public int BillCount { get; set; }
    public int InvoiceCount { get; set; }
    public int PurchaseCount { get; set; }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Sprint 8: E-Invoice generation service.
/// Generates the NIC-compliant JSON payload, computes the IRN hash,
/// and stores the record. In production, this would call the NIC API;
/// in development mode it simulates the acknowledgement locally.
/// </summary>
public class EInvoiceService
{
    private readonly ApplicationDbContext _db;

    public EInvoiceService(ApplicationDbContext db) => _db = db;

    /// <summary>Generate an E-Invoice for a completed POS bill.</summary>
    public async Task<EInvoice> GenerateForPosBillAsync(Guid posBillId)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.PosBillId == posBillId)
            ?? throw new InvalidOperationException("POS Bill not found.");

        if (bill.Status != 2)
            throw new InvalidOperationException("Only completed bills can generate E-Invoices.");

        var existing = await _db.EInvoices
            .AnyAsync(e => e.PosBillId == posBillId && e.Status == 1);
        if (existing)
            throw new InvalidOperationException("An active E-Invoice already exists for this bill.");

        var company = bill.CompanyId.HasValue
            ? await _db.Set<Company>().AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == bill.CompanyId)
            : null;

        var supplierGstin = bill.Store?.GstNo ?? company?.GstNo ?? "";
        var payload = BuildPayload(supplierGstin, bill.Customer, bill.BillNo, bill.BillDate, bill.Lines, bill.GrandTotal, bill.TaxTotal);
        var irn = ComputeIrn(supplierGstin, bill.BillNo, bill.BillDate.ToString("yyyy-MM-dd"));

        var einvoice = new EInvoice
        {
            PosBillId = posBillId,
            Irn = irn,
            AckNo = GenerateAckNo(),
            AckDate = DateTime.UtcNow,
            SignedInvoice = payload,
            SignedQrCode = GenerateQrPayload(irn, supplierGstin, bill.Customer?.Gstin, bill.GrandTotal),
            Status = 1,
            GeneratedAtUtc = DateTime.UtcNow,
            CompanyId = bill.CompanyId
        };

        _db.EInvoices.Add(einvoice);
        await _db.SaveChangesAsync();
        return einvoice;
    }

    /// <summary>Generate an E-Invoice for a posted Invoice.</summary>
    public async Task<EInvoice> GenerateForInvoiceAsync(Guid invoiceId)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status != 2)
            throw new InvalidOperationException("Only posted invoices can generate E-Invoices.");

        var existing = await _db.EInvoices
            .AnyAsync(e => e.InvoiceId == invoiceId && e.Status == 1);
        if (existing)
            throw new InvalidOperationException("An active E-Invoice already exists for this invoice.");

        var company = invoice.CompanyId.HasValue
            ? await _db.Set<Company>().AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == invoice.CompanyId)
            : null;

        var supplierGstin = company?.GstNo ?? "";
        var taxTotal = invoice.Lines.Sum(l =>
            l.GstPercentSnapshot.HasValue
                ? (l.Qty * l.UnitPrice - l.DiscountAmount) * l.GstPercentSnapshot.Value / 100m
                : 0);

        var payload = BuildInvoicePayload(supplierGstin, invoice, taxTotal);
        var irn = ComputeIrn(supplierGstin, invoice.InvoiceNo, invoice.InvoiceDate.ToString("yyyy-MM-dd"));

        var einvoice = new EInvoice
        {
            InvoiceId = invoiceId,
            Irn = irn,
            AckNo = GenerateAckNo(),
            AckDate = DateTime.UtcNow,
            SignedInvoice = payload,
            SignedQrCode = GenerateQrPayload(irn, supplierGstin, invoice.Customer?.Gstin, invoice.TotalAmount),
            Status = 1,
            GeneratedAtUtc = DateTime.UtcNow,
            CompanyId = invoice.CompanyId
        };

        _db.EInvoices.Add(einvoice);
        await _db.SaveChangesAsync();
        return einvoice;
    }

    /// <summary>Cancel an E-Invoice (within 24 hours per GST rules).</summary>
    public async Task CancelAsync(Guid eInvoiceId, string reason)
    {
        var einv = await _db.EInvoices.FindAsync(eInvoiceId)
            ?? throw new InvalidOperationException("E-Invoice not found.");

        if (einv.Status != 1)
            throw new InvalidOperationException("Only active E-Invoices can be cancelled.");

        if (DateTime.UtcNow - einv.GeneratedAtUtc > TimeSpan.FromHours(24))
            throw new InvalidOperationException("E-Invoice can only be cancelled within 24 hours of generation.");

        einv.Status = 2; // Cancelled
        einv.CancelReason = reason;
        einv.CancelledAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // E-Way Bill
    // ═══════════════════════════════════════════════════════════

    /// <summary>Generate an E-Way Bill for a POS bill with value > ₹50,000.</summary>
    public async Task<EWayBill> GenerateEWayBillAsync(Guid posBillId, EWayBillInput input)
    {
        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .FirstOrDefaultAsync(b => b.PosBillId == posBillId)
            ?? throw new InvalidOperationException("POS Bill not found.");

        var company = bill.CompanyId.HasValue
            ? await _db.Set<Company>().AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == bill.CompanyId)
            : null;

        var ewb = new EWayBill
        {
            EwbNo = GenerateEwbNo(),
            PosBillId = posBillId,
            SupplierGstin = bill.Store?.GstNo ?? company?.GstNo ?? "",
            RecipientGstin = bill.Customer?.Gstin,
            DocType = "INV",
            DocNo = bill.BillNo,
            DocDate = bill.BillDate,
            TotalValue = bill.GrandTotal,
            CgstAmount = bill.TaxTotal / 2,
            SgstAmount = bill.TaxTotal / 2,
            IgstAmount = 0,
            TransporterId = input.TransporterId,
            TransporterName = input.TransporterName,
            VehicleNo = input.VehicleNo,
            TransMode = input.TransMode ?? "Road",
            Distance = input.Distance,
            FromAddress = bill.Store?.Address,
            FromPincode = null,
            ToAddress = input.ToAddress,
            ToPincode = input.ToPincode,
            ValidUpto = DateTime.UtcNow.AddDays(input.Distance <= 100 ? 1 : (int)Math.Ceiling(input.Distance / 100m)),
            Status = 1,
            CompanyId = bill.CompanyId
        };

        _db.EWayBills.Add(ewb);
        await _db.SaveChangesAsync();
        return ewb;
    }

    public async Task CancelEWayBillAsync(Guid ewayBillId, string reason)
    {
        var ewb = await _db.EWayBills.FindAsync(ewayBillId)
            ?? throw new InvalidOperationException("E-Way Bill not found.");

        if (ewb.Status != 1)
            throw new InvalidOperationException("Only active E-Way Bills can be cancelled.");

        ewb.Status = 2;
        ewb.CancelReason = reason;
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Compute IRN as SHA-256 hash of SupplierGSTIN + DocNo + FY (NIC standard).
    /// </summary>
    private static string ComputeIrn(string supplierGstin, string docNo, string docDate)
    {
        var fy = GetFinancialYear(DateTime.Parse(docDate));
        var raw = $"{supplierGstin}{docNo}{fy}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static string GetFinancialYear(DateTime date)
    {
        var startYear = date.Month >= 4 ? date.Year : date.Year - 1;
        return $"{startYear}-{(startYear + 1) % 100:D2}";
    }

    private static string GenerateAckNo()
        => $"1{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";

    private static string GenerateEwbNo()
        => $"3{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(10000, 99999)}";

    private static string GenerateQrPayload(string irn, string sellerGstin, string? buyerGstin, decimal total)
    {
        var qr = new
        {
            SellerGstin = sellerGstin,
            BuyerGstin = buyerGstin ?? "",
            DocNo = irn[..20],
            TotInvVal = total,
            Irn = irn,
            IrnDt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };
        return JsonSerializer.Serialize(qr);
    }

    private static string BuildPayload(string supplierGstin, Customer? customer,
        string docNo, DateTime docDate, List<PosBillLine> lines, decimal grandTotal, decimal taxTotal)
    {
        var payload = new
        {
            Version = "1.1",
            TranDtls = new { TaxSch = "GST", SupTyp = customer?.Gstin != null ? "B2B" : "B2C", RegRev = "N", IgstOnIntra = "N" },
            DocDtls = new { Typ = "INV", No = docNo, Dt = docDate.ToString("dd/MM/yyyy") },
            SellerDtls = new { Gstin = supplierGstin },
            BuyerDtls = new { Gstin = customer?.Gstin ?? "URP", Nm = customer?.Name ?? "Walk-in", Addr1 = customer?.Address ?? "", Stcd = "07" },
            ItemList = lines.Select((l, i) => new
            {
                SlNo = (i + 1).ToString(),
                PrdDesc = l.ItemNameSnapshot ?? "Item",
                HsnCd = l.HsnCodeSnapshot ?? "0000",
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                TotAmt = l.LineTotal,
                GstRt = l.GstPercentSnapshot ?? 0,
                CgstAmt = l.GstPercentSnapshot.HasValue ? l.LineTotal * l.GstPercentSnapshot.Value / 200m : 0,
                SgstAmt = l.GstPercentSnapshot.HasValue ? l.LineTotal * l.GstPercentSnapshot.Value / 200m : 0,
                IgstAmt = 0
            }),
            ValDtls = new { TotInvVal = grandTotal, CgstVal = taxTotal / 2, SgstVal = taxTotal / 2, IgstVal = 0 }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildInvoicePayload(string supplierGstin, Invoice invoice, decimal taxTotal)
    {
        var payload = new
        {
            Version = "1.1",
            TranDtls = new { TaxSch = "GST", SupTyp = invoice.Customer?.Gstin != null ? "B2B" : "B2C", RegRev = "N" },
            DocDtls = new { Typ = "INV", No = invoice.InvoiceNo, Dt = invoice.InvoiceDate.ToString("dd/MM/yyyy") },
            SellerDtls = new { Gstin = supplierGstin },
            BuyerDtls = new { Gstin = invoice.Customer?.Gstin ?? "URP", Nm = invoice.Customer?.Name ?? "Walk-in" },
            ItemList = invoice.Lines.Select((l, i) => new
            {
                SlNo = (i + 1).ToString(),
                PrdDesc = l.ItemNameSnapshot ?? "Item",
                HsnCd = l.HsnCodeSnapshot ?? "0000",
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                TotAmt = l.Qty * l.UnitPrice - l.DiscountAmount,
                GstRt = l.GstPercentSnapshot ?? 0,
                CgstAmt = l.GstPercentSnapshot.HasValue ? (l.Qty * l.UnitPrice - l.DiscountAmount) * l.GstPercentSnapshot.Value / 200m : 0,
                SgstAmt = l.GstPercentSnapshot.HasValue ? (l.Qty * l.UnitPrice - l.DiscountAmount) * l.GstPercentSnapshot.Value / 200m : 0,
            }),
            ValDtls = new { TotInvVal = invoice.TotalAmount, CgstVal = taxTotal / 2, SgstVal = taxTotal / 2, IgstVal = 0 }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class EWayBillInput
{
    public string? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? VehicleNo { get; set; }
    public string? TransMode { get; set; }
    public decimal Distance { get; set; }
    public string? ToAddress { get; set; }
    public string? ToPincode { get; set; }
}

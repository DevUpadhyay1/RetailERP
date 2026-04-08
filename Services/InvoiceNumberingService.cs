using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Generates tenant/store-aware invoice numbers by document type.
/// Supports company default rule with optional store-level overrides.
/// </summary>
public class InvoiceNumberingService
{
    private readonly ApplicationDbContext _db;

    public InvoiceNumberingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateNextInvoiceNoAsync(
        Guid companyId,
        Guid? storeId,
        InvoiceDocumentType documentType,
        DateTime invoiceDate)
    {
        // Prefer store-specific active rule, fallback to company-level active rule.
        InvoiceNumberingRule? rule = null;
        if (storeId.HasValue)
        {
            rule = await _db.InvoiceNumberingRules
                .FirstOrDefaultAsync(r =>
                    r.CompanyId == companyId &&
                    r.StoreId == storeId &&
                    r.DocumentType == documentType &&
                    r.IsActive);
        }

        rule ??= await _db.InvoiceNumberingRules
            .FirstOrDefaultAsync(r =>
                r.CompanyId == companyId &&
                r.StoreId == null &&
                r.DocumentType == documentType &&
                r.IsActive);

        if (rule is null)
        {
            rule = new InvoiceNumberingRule
            {
                InvoiceNumberingRuleId = Guid.NewGuid(),
                CompanyId = companyId,
                StoreId = null, // default company-wide rule
                DocumentType = documentType,
                Prefix = GetDefaultPrefix(documentType),
                NumberWidth = 4,
                NextNumber = 1,
                ResetPolicy = InvoiceNumberResetPolicy.Yearly,
                LastResetAtUtc = DateTime.SpecifyKind(invoiceDate.Date, DateTimeKind.Utc),
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _db.InvoiceNumberingRules.Add(rule);
            await _db.SaveChangesAsync();
        }

        ApplyResetIfNeeded(rule, invoiceDate);

        var current = rule.NextNumber;
        rule.NextNumber = current + 1;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var width = Math.Clamp(rule.NumberWidth, 3, 8);
        var serial = current.ToString($"D{width}");
        var prefix = (rule.Prefix ?? string.Empty).Trim().ToUpperInvariant();
        var suffix = string.IsNullOrWhiteSpace(rule.Suffix) ? string.Empty : "-" + rule.Suffix.Trim().ToUpperInvariant();

        var period = rule.ResetPolicy switch
        {
            InvoiceNumberResetPolicy.Yearly => invoiceDate.ToString("yyyy"),
            InvoiceNumberResetPolicy.Monthly => invoiceDate.ToString("yyyyMM"),
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(period)
            ? $"{prefix}-{serial}{suffix}"
            : $"{prefix}-{period}-{serial}{suffix}";
    }

    private static void ApplyResetIfNeeded(InvoiceNumberingRule rule, DateTime invoiceDate)
    {
        var periodDate = invoiceDate.Date;
        var last = (rule.LastResetAtUtc ?? DateTime.SpecifyKind(periodDate, DateTimeKind.Utc)).Date;

        var shouldReset = rule.ResetPolicy switch
        {
            InvoiceNumberResetPolicy.Never => false,
            InvoiceNumberResetPolicy.Yearly => last.Year != periodDate.Year,
            InvoiceNumberResetPolicy.Monthly => last.Year != periodDate.Year || last.Month != periodDate.Month,
            _ => false
        };

        if (!shouldReset) return;

        rule.NextNumber = 1;
        rule.LastResetAtUtc = DateTime.SpecifyKind(periodDate, DateTimeKind.Utc);
    }

    private static string GetDefaultPrefix(InvoiceDocumentType type) => type switch
    {
        InvoiceDocumentType.TaxInvoice => "TAX",
        InvoiceDocumentType.BillOfSupply => "BOS",
        InvoiceDocumentType.CreditNote => "CN",
        InvoiceDocumentType.DebitNote => "DN",
        InvoiceDocumentType.ProformaInvoice => "PRO",
        _ => "INV"
    };
}

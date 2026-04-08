using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

/// <summary>
/// Tenant-owned numbering rules per invoice document type,
/// optionally overridden at store level.
/// </summary>
public class InvoiceNumberingRule : ITenantEntity
{
    [Key]
    public Guid InvoiceNumberingRuleId { get; set; } = Guid.NewGuid();

    public Guid? CompanyId { get; set; }

    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.TaxInvoice;

    [Required, StringLength(20)]
    public string Prefix { get; set; } = "INV";

    [StringLength(20)]
    public string? Suffix { get; set; }

    /// <summary>Number of digits in sequence section.</summary>
    [Range(3, 8)]
    public int NumberWidth { get; set; } = 4;

    /// <summary>Next sequence number to issue.</summary>
    [Range(1, int.MaxValue)]
    public int NextNumber { get; set; } = 1;

    public InvoiceNumberResetPolicy ResetPolicy { get; set; } = InvoiceNumberResetPolicy.Yearly;

    public DateTime? LastResetAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

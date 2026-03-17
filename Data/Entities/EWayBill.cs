using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 8: GST E-Way Bill for goods movement exceeding ₹50,000.
/// Stores Part-A (invoice/consignment) and Part-B (transport) details.
/// Status: 1 = Active, 2 = Cancelled, 3 = Expired.
/// </summary>
public class EWayBill : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid EWayBillId { get; set; } = Guid.NewGuid();

    /// <summary>12-digit E-Way Bill number.</summary>
    [Required, StringLength(20)]
    public string EwbNo { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ValidUpto { get; set; }

    /// <summary>1 = Active, 2 = Cancelled, 3 = Expired.</summary>
    public byte Status { get; set; } = 1;

    // ─── Source document ───
    public Guid? PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    // ─── Part A: Consignment details ───
    [Required, StringLength(15)]
    public string SupplierGstin { get; set; } = string.Empty;

    [StringLength(15)]
    public string? RecipientGstin { get; set; }

    [StringLength(10)]
    public string DocType { get; set; } = "INV"; // INV, CHL, BIL, BOE, OTH

    [StringLength(30)]
    public string? DocNo { get; set; }

    public DateTime? DocDate { get; set; }

    [Precision(18, 2)]
    public decimal TotalValue { get; set; }

    [Precision(18, 2)]
    public decimal CgstAmount { get; set; }

    [Precision(18, 2)]
    public decimal SgstAmount { get; set; }

    [Precision(18, 2)]
    public decimal IgstAmount { get; set; }

    // ─── Part B: Transport details ───
    [StringLength(15)]
    public string? TransporterId { get; set; }

    [StringLength(200)]
    public string? TransporterName { get; set; }

    [StringLength(20)]
    public string? VehicleNo { get; set; }

    [StringLength(10)]
    public string TransMode { get; set; } = "Road"; // Road, Rail, Air, Ship

    [Precision(10, 2)]
    public decimal Distance { get; set; }

    // ─── Addresses ───
    [StringLength(300)]
    public string? FromAddress { get; set; }

    [StringLength(6)]
    public string? FromPincode { get; set; }

    [StringLength(300)]
    public string? ToAddress { get; set; }

    [StringLength(6)]
    public string? ToPincode { get; set; }

    [StringLength(500)]
    public string? CancelReason { get; set; }

    // Tenant
    public Guid? CompanyId { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

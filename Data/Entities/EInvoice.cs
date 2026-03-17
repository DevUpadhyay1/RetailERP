using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 8: GST E-Invoice record. Stores the IRN (Invoice Reference Number),
/// acknowledgement details and signed QR code returned by the NIC portal.
/// Status: 1 = Generated, 2 = Cancelled, 3 = Failed.
/// </summary>
public class EInvoice : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid EInvoiceId { get; set; } = Guid.NewGuid();

    /// <summary>FK to the PosBill or Invoice this E-Invoice covers.</summary>
    public Guid? PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>64-character IRN hash returned by NIC.</summary>
    [Required, StringLength(64)]
    public string Irn { get; set; } = string.Empty;

    [StringLength(20)]
    public string? AckNo { get; set; }

    public DateTime? AckDate { get; set; }

    /// <summary>The full signed JSON payload returned by NIC.</summary>
    public string? SignedInvoice { get; set; }

    /// <summary>Signed QR code string for printing on the invoice.</summary>
    public string? SignedQrCode { get; set; }

    /// <summary>1 = Generated, 2 = Cancelled, 3 = Failed.</summary>
    public byte Status { get; set; } = 1;

    [StringLength(500)]
    public string? CancelReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    // Tenant
    public Guid? CompanyId { get; set; }

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// Maker-checker flow for sensitive stock edits.
/// Non-admin users can request adjustments; only Admin can approve and apply.
/// </summary>
public class StockAdjustmentRequest : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid StockAdjustmentRequestId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StockId { get; set; }
    public Stock? Stock { get; set; }

    [Precision(18, 2)]
    [Range(-999999999, 999999999)]
    public decimal DeltaQty { get; set; }

    [Required, MaxLength(300)]
    public string Reason { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }
    public ApplicationUser? RequestedByUser { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    // 1=Pending, 2=Approved, 3=Rejected, 4=Cancelled
    public byte Status { get; set; } = 1;

    public Guid? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(500)]
    public string? ReviewNote { get; set; }

    public Guid? AppliedStockTransactionId { get; set; }

    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

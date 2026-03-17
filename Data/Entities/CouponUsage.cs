using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 6: Tracks which bill used which coupon and how much discount was applied.
/// </summary>
public class CouponUsage : IAuditableEntity
{
    [Key]
    public Guid CouponUsageId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CouponId { get; set; }
    public Coupon? Coupon { get; set; }

    [Required]
    public Guid PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    [Precision(18, 2)]
    public decimal DiscountApplied { get; set; }

    public DateTime UsedAtUtc { get; set; } = DateTime.UtcNow;

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

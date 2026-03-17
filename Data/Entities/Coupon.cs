using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 6: Discount coupon. Can be Percent or Flat amount.
/// Applied at the bill level during POS billing.
/// </summary>
public class Coupon : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid CouponId { get; set; } = Guid.NewGuid();

    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Description { get; set; }

    // "Percent" or "Flat"
    [Required, StringLength(10)]
    public string DiscountType { get; set; } = "Percent";

    [Precision(18, 2)]
    [Range(0.01, 999999999)]
    public decimal DiscountValue { get; set; }

    [Precision(18, 2)]
    public decimal MinBillAmount { get; set; }

    // Cap for percentage discounts (0 = no cap)
    [Precision(18, 2)]
    public decimal MaxDiscount { get; set; }

    [DataType(DataType.Date)]
    public DateTime ValidFrom { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime ValidTo { get; set; } = DateTime.Today.AddMonths(1);

    public int MaxUses { get; set; } = 0; // 0 = unlimited
    public int UsedCount { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<CouponUsage> Usages { get; set; } = new();
}

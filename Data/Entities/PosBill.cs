using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// DMART-style POS bill header. One per customer transaction at the point of sale.
/// Status: 1 = Open (cashier still adding lines), 2 = Completed (paid &amp; stock deducted), 3 = Cancelled.
/// </summary>
public class PosBill : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PosBillId { get; set; } = Guid.NewGuid();

    [Required, StringLength(30)]
    public string BillNo { get; set; } = string.Empty;

    [Required]
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    // Walk-in customer = null; otherwise linked customer for loyalty, etc.
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    // Phase 6: Loyalty & Coupon links
    public Guid? LoyaltyCardId { get; set; }
    public LoyaltyCard? LoyaltyCard { get; set; }

    public Guid? CouponId { get; set; }
    public Coupon? Coupon { get; set; }

    [Precision(18, 2)]
    public decimal CouponDiscount { get; set; }

    [Precision(18, 2)]
    public decimal LoyaltyPointsRedeemed { get; set; }

    [Precision(18, 2)]
    public decimal LoyaltyDiscount { get; set; }

    // The cashier / POS operator
    public Guid? CashierUserId { get; set; }
    public ApplicationUser? CashierUser { get; set; }

    [DataType(DataType.Date)]
    public DateTime BillDate { get; set; } = DateTime.Today;

    [Precision(18, 2)]
    public decimal SubTotal { get; set; }

    [Precision(18, 2)]
    public decimal TaxTotal { get; set; }

    [Precision(18, 2)]
    public decimal DiscountTotal { get; set; }

    // Sprint 7: Bill-level additional discount / charge / round-off
    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal AddDiscountPercent { get; set; }

    [Precision(18, 2)]
    public decimal AddDiscountAmount { get; set; }

    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal AddChargePercent { get; set; }

    [Precision(18, 2)]
    public decimal AddChargeAmount { get; set; }

    [Precision(18, 2)]
    public decimal RoundOff { get; set; }

    [Precision(18, 2)]
    public decimal GrandTotal { get; set; }

    // Sprint 7: Invoice type — "Retail" or "Tax"
    [StringLength(10)]
    public string InvoiceType { get; set; } = "Retail";

    [StringLength(500)]
    public string? Remark { get; set; }

    // 1 = Open, 2 = Completed, 3 = Cancelled, 4 = OnHold
    public byte Status { get; set; } = 1;
    public DateTime? CompletedAtUtc { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<PosBillLine> Lines { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

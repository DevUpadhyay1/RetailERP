using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// One line item on a POS bill. Snapshot fields freeze price/name at time of sale.
/// </summary>
public class PosBillLine : IAuditableEntity
{
    [Key]
    public Guid PosBillLineId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    // Snapshot at time of billing
    [StringLength(50)]
    public string? BarcodeSnapshot { get; set; }

    [StringLength(50)]
    public string? SkuSnapshot { get; set; }

    [StringLength(200)]
    public string? ItemNameSnapshot { get; set; }

    [Precision(18, 2)]
    [Range(0.0001, 999999999)]
    public decimal Qty { get; set; } = 1;

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? MrpSnapshot { get; set; }

    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal? GstPercentSnapshot { get; set; }

    [StringLength(20)]
    public string? HsnCodeSnapshot { get; set; }

    // Sprint 7: Line-level discount support
    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Precision(18, 2)]
    public decimal DiscountAmount { get; set; }

    /// <summary>Effective unit price after line discount: UnitPrice × (1 - Disc%/100)</summary>
    [Precision(18, 2)]
    public decimal NetRate { get; set; }

    [Precision(18, 2)]
    public decimal LineTotal { get; set; }

    // Sprint 7: Promotion that applied this discount (null = manual discount)
    public Guid? AppliedPromotionId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

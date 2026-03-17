using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 7: Promotion / discount scheme.
/// Types: FlatPercent, FlatAmount, BOGO, BuyXGetY, ComboDiscount, HappyHour.
/// </summary>
public class Promotion : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PromotionId { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// FlatPercent  — e.g. 10% off on item/category
    /// FlatAmount   — e.g. ₹50 off on item/category
    /// BOGO         — Buy One Get One free (or Buy X Get Y)
    /// BuyXGetY     — Buy X items, get Y items free/discounted
    /// ComboDiscount— Buy items A+B together for a fixed combo price
    /// HappyHour    — Time-based pricing (active only during time window)
    /// </summary>
    [Required, StringLength(20)]
    public string PromoType { get; set; } = "FlatPercent";

    // ─── Discount values ───
    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Precision(18, 2)]
    public decimal DiscountAmount { get; set; }

    // ─── Scope: which items/categories does this apply to? ───
    /// <summary>null = applies to all items; set = applies to specific item only.</summary>
    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>null = no category filter; set = applies to all items in category.</summary>
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    // ─── BOGO / BuyXGetY rules ───
    /// <summary>Buy this many items to trigger (e.g., Buy 2)</summary>
    public int BuyQty { get; set; }

    /// <summary>Get this many free/discounted (e.g., Get 1)</summary>
    public int GetQty { get; set; }

    /// <summary>For BuyXGetY: the free item (null = same item)</summary>
    public Guid? FreeItemId { get; set; }
    public Item? FreeItem { get; set; }

    // ─── Combo rules ───
    /// <summary>JSON array of item IDs that form the combo, e.g. ["guid1","guid2"]</summary>
    [StringLength(2000)]
    public string? ComboItemIds { get; set; }

    /// <summary>Fixed combo price when all combo items present</summary>
    [Precision(18, 2)]
    public decimal ComboPrice { get; set; }

    // ─── Validity ───
    [DataType(DataType.Date)]
    public DateTime ValidFrom { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime ValidTo { get; set; } = DateTime.Today.AddMonths(1);

    /// <summary>HappyHour: start time of day (e.g., 14:00)</summary>
    public TimeSpan? HappyHourStart { get; set; }

    /// <summary>HappyHour: end time of day (e.g., 16:00)</summary>
    public TimeSpan? HappyHourEnd { get; set; }

    // ─── Constraints ───
    [Precision(18, 2)]
    public decimal MinBillAmount { get; set; }

    public int MaxUsesTotal { get; set; }  // 0 = unlimited
    public int UsedCount { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Priority for applying promotions (lower = applied first)</summary>
    public int Priority { get; set; } = 100;

    /// <summary>If true, cannot be combined with other promotions on same line</summary>
    public bool IsExclusive { get; set; }

    // ─── Tenant ───
    public Guid? CompanyId { get; set; }

    // ─── Audit ───
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

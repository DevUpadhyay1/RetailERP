using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Item : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid ItemId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; } = 0;

    // --- DMART minimum (Phase 1) ---
    [StringLength(50)]
    public string? Barcode { get; set; }

    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? MRP { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? PurchasePrice { get; set; }

    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal? GstPercent { get; set; }

    [StringLength(20)]
    public string? HsnCode { get; set; }

    [Range(0, 999999)]
    public int ReorderLevel { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

}
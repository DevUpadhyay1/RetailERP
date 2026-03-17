using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class PurchaseLine : IAuditableEntity
{
    [Key]
    public Guid PurchaseLineId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Precision(18, 2)]
    [Range(0.0001, 999999999)]
    public decimal Qty { get; set; } = 1;

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitCost { get; set; } = 0;

    // Snapshot fields
    [StringLength(50)]
    public string? ItemSkuSnapshot { get; set; }

    [StringLength(200)]
    public string? ItemNameSnapshot { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

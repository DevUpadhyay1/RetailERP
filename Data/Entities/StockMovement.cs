using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class StockMovement : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid StockMovementId { get; set; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(50)]
    public string MovementType { get; set; } = string.Empty; // e.g., PurchaseReceived, InvoicePosted, Adjustment

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Precision(18, 2)]
    [Range(-999999999, 999999999)]
    public decimal QuantityChange { get; set; }

    // Optional references for traceability
    public Guid? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? UnitCost { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? UnitPrice { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

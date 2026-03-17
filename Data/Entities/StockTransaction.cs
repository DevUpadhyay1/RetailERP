using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

public class StockTransaction : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid StockTransactionId { get; set; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // DMART ledger types: IN / OUT / ADJUSTMENT / TRANSFER / RETURN
    [Required, StringLength(20)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    // Keep Warehouse as the primary locator; Store is optional (Warehouse already maps to Store).
    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    // Signed quantity change (+ increases stock, - decreases stock)
    [Precision(18, 2)]
    [Range(-999999999, 999999999)]
    public decimal Qty { get; set; }

    // Generic reference for traceability (e.g. Purchase/Invoice/StockAdjust/StockTransfer)
    [StringLength(50)]
    public string? RefType { get; set; }

    [StringLength(64)]
    public string? RefId { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? UnitCost { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal? UnitPrice { get; set; }

    // Explicit actor field (in addition to the generic CreatedByUserId audit field)
    public Guid? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Stock : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid StockId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal Quantity { get; set; } = 0;

    // Sprint 7: Batch expiry tracking
    [StringLength(50)]
    public string? BatchNumber { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ManufactureDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
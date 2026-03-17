using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Purchase : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PurchaseId { get; set; } = Guid.NewGuid();

    [Required, StringLength(32)]
    public string PurchaseNo { get; set; } = string.Empty;

    [Required]
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // 1 = Draft, 2 = Received
    public byte Status { get; set; } = 1;

    public DateTime? ReceivedAt { get; set; }

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; } = 0;

    [StringLength(500)]
    public string? Notes { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<PurchaseLine> Lines { get; set; } = new();
}

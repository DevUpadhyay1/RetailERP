using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Supplier response to a Purchase Order exposed in supplier portal.
/// ResponseStatus: 1 = Pending, 2 = Accepted, 3 = Rejected.
/// </summary>
public class SupplierPoResponse : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid SupplierPoResponseId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    [Required]
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public byte ResponseStatus { get; set; } = 1;
    public DateTime? RespondedAtUtc { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    [StringLength(500)]
    public string? SupplierNote { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

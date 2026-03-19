using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// One-time/expiring external access link for Customer/Supplier self-service portals.
/// PortalType: 1 = Customer, 2 = Supplier.
/// </summary>
public class PortalAccessLink : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PortalAccessLinkId { get; set; } = Guid.NewGuid();

    public byte PortalType { get; set; } = 1;

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Required, StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [StringLength(12)]
    public string? TokenHint { get; set; }

    [StringLength(120)]
    public string? Label { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? LastAccessedAtUtc { get; set; }
    public bool IsRevoked { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

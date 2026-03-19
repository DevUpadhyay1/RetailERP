using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// Customer-submitted online return request from customer portal.
/// Status: 1 = Requested, 2 = Approved, 3 = Rejected, 4 = Processed.
/// </summary>
public class PortalReturnRequest : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PortalReturnRequestId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required]
    public Guid PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Reason { get; set; }

    public byte Status { get; set; } = 1;

    [StringLength(500)]
    public string? AdminNote { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }

    public Guid? PosReturnId { get; set; }
    public PosReturn? PosReturn { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// Request raised by a brand-owner company admin for mapping a franchise operator company.
/// SuperAdmin reviews and approves/rejects the request.
/// </summary>
public class FranchiseMappingRequest : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid FranchiseMappingRequestId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid RequestingCompanyId { get; set; }
    public Company? RequestingCompany { get; set; }

    public Guid? MappedOperatorCompanyId { get; set; }
    public Company? MappedOperatorCompany { get; set; }

    [Required, StringLength(200)]
    public string RequestedOperatorName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? RequestedOperatorCode { get; set; }

    [StringLength(100)]
    public string? RequestedOperatorCity { get; set; }

    [StringLength(100)]
    public string? RequestedOperatorState { get; set; }

    [StringLength(500)]
    public string? RequestNote { get; set; }

    // 1=Pending, 2=Approved, 3=Rejected, 4=Cancelled
    public byte Status { get; set; } = 1;

    public Guid? RequestedByUserId { get; set; }
    public ApplicationUser? RequestedByUser { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    [StringLength(500)]
    public string? ReviewNote { get; set; }

    // Tenant scope key (same as requesting company)
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Supplier : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid SupplierId { get; set; } = Guid.NewGuid();

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

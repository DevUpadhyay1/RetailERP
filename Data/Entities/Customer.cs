using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Customer : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid CustomerId { get; set; } = Guid.NewGuid();

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    // Sprint 7: B2B invoice fields
    [StringLength(15, MinimumLength = 15, ErrorMessage = "GSTIN must be exactly 15 characters")]
    [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Enter a valid GSTIN (e.g. 29ABCDE1234F1Z5)")]
    public string? Gstin { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(50)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN code must be exactly 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN code must be exactly 6 digits")]
    public string? PinCode { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
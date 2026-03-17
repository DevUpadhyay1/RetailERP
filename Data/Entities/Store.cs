using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Store : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid StoreId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string StoreCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(15, MinimumLength = 15, ErrorMessage = "GSTIN must be exactly 15 characters")]
    [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Enter a valid GSTIN (e.g. 22AAAAA0000A1Z5)")]
    public string? GstNo { get; set; }

    [StringLength(10, MinimumLength = 10, ErrorMessage = "PAN must be exactly 10 characters")]
    [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Enter a valid PAN (e.g. ABCDE1234F)")]
    public string? PanNo { get; set; }

    public bool IsActive { get; set; } = true;

    // Sprint 3 – Business type for dashboard defaults
    public BusinessType BusinessType { get; set; } = BusinessType.Other;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

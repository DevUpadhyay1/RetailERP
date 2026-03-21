using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

public class Company
{
    [Key]
    public Guid CompanyId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Sprint 4.1 – Primary business type for this company.</summary>
    public BusinessType BusinessType { get; set; } = BusinessType.Other;

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? City { get; set; }

    [StringLength(20)]
    public string? State { get; set; }

    [StringLength(6, MinimumLength = 6, ErrorMessage = "PIN code must be exactly 6 digits")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "PIN code must be exactly 6 digits")]
    public string? Pincode { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(200)]
    public string? Website { get; set; }

    [StringLength(15, MinimumLength = 15, ErrorMessage = "GSTIN must be exactly 15 characters")]
    [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Enter a valid GSTIN (e.g. 22AAAAA0000A1Z5)")]
    public string? GstNo { get; set; }

    [StringLength(10, MinimumLength = 10, ErrorMessage = "PAN must be exactly 10 characters")]
    [RegularExpression(@"^[A-Z]{5}[0-9]{4}[A-Z]{1}$", ErrorMessage = "Enter a valid PAN (e.g. ABCDE1234F)")]
    public string? PanNo { get; set; }

    [StringLength(30)]
    public string? CinNo { get; set; }

    /// <summary>Sprint 6 – Relative path to uploaded company logo.</summary>
    [StringLength(300)]
    public string? LogoPath { get; set; }

    /// <summary>Sprint 16 – Multi-tenant gateway selection.</summary>
    public PaymentGatewayProvider GatewayProvider { get; set; } = PaymentGatewayProvider.None;

    [StringLength(100)]
    public string? GatewayKeyId { get; set; }

    [StringLength(100)]
    public string? GatewayKeySecret { get; set; }

    /// <summary>Max users allowed for this tenant (0 = unlimited).</summary>
    public int MaxUsers { get; set; } = 0;

    /// <summary>Max stores allowed for this tenant (0 = unlimited).</summary>
    public int MaxStores { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    /// <summary>Sprint 15 – Parent company for franchise hierarchy (null = independent / franchisor).</summary>
    public Guid? ParentCompanyId { get; set; }
    public Company? ParentCompany { get; set; }
    public ICollection<Company> ChildCompanies { get; set; } = new List<Company>();

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>Sprint 15 – Links a franchisor (parent company) to a franchisee (child company).</summary>
public class FranchiseAgreement : IAuditableEntity
{
    [Key]
    public Guid FranchiseAgreementId { get; set; } = Guid.NewGuid();

    /// <summary>The franchisor / brand-owner company.</summary>
    public Guid FranchisorCompanyId { get; set; }
    public Company? FranchisorCompany { get; set; }

    /// <summary>The franchisee / operator company.</summary>
    public Guid FranchiseeCompanyId { get; set; }
    public Company? FranchiseeCompany { get; set; }

    [Required, StringLength(100)]
    public string AgreementCode { get; set; } = string.Empty;

    /// <summary>Royalty as a percentage of gross sales (e.g. 5.00 = 5%).</summary>
    public decimal RoyaltyPercent { get; set; }

    /// <summary>Flat monthly fee charged in addition to royalty (0 if none).</summary>
    public decimal MonthlyFlatFee { get; set; }

    /// <summary>Minimum monthly royalty regardless of sales (floor).</summary>
    public decimal MinMonthlyRoyalty { get; set; }

    [StringLength(50)]
    public string Territory { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>1=Active, 2=Expired, 3=Terminated</summary>
    public byte Status { get; set; } = 1;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public ICollection<RoyaltyPayment> RoyaltyPayments { get; set; } = new List<RoyaltyPayment>();
}

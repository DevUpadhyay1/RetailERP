using System.ComponentModel.DataAnnotations;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>Sprint 15 – Records royalty payments from franchisee to franchisor.</summary>
public class RoyaltyPayment : IAuditableEntity
{
    [Key]
    public Guid RoyaltyPaymentId { get; set; } = Guid.NewGuid();

    public Guid FranchiseAgreementId { get; set; }
    public FranchiseAgreement? Agreement { get; set; }

    /// <summary>Period year (e.g. 2026).</summary>
    public int PeriodYear { get; set; }

    /// <summary>Period month (1-12).</summary>
    public byte PeriodMonth { get; set; }

    /// <summary>Gross sales for the period used to calculate royalty.</summary>
    public decimal GrossSales { get; set; }

    /// <summary>Royalty amount calculated (percent-based portion).</summary>
    public decimal RoyaltyAmount { get; set; }

    /// <summary>Flat fee added for the period.</summary>
    public decimal FlatFeeAmount { get; set; }

    /// <summary>Total due = max(RoyaltyAmount + FlatFeeAmount, MinMonthlyRoyalty).</summary>
    public decimal TotalDue { get; set; }

    /// <summary>Actual amount paid by franchisee.</summary>
    public decimal AmountPaid { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    /// <summary>1=Pending, 2=Paid, 3=Overdue, 4=Waived</summary>
    public byte Status { get; set; } = 1;

    [StringLength(500)]
    public string? Remarks { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

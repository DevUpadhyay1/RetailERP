using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 6: Customer loyalty membership card.
/// Points are earned when bills are completed and can be redeemed for discounts.
/// Tier: 1 = Bronze, 2 = Silver, 3 = Gold, 4 = Platinum.
/// </summary>
public class LoyaltyCard : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid LoyaltyCardId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required, StringLength(30)]
    public string CardNumber { get; set; } = string.Empty;

    [Precision(18, 2)]
    public decimal PointsBalance { get; set; }

    [Precision(18, 2)]
    public decimal LifetimePoints { get; set; }

    // 1 = Bronze, 2 = Silver, 3 = Gold, 4 = Platinum
    public byte Tier { get; set; } = 1;

    [DataType(DataType.Date)]
    public DateTime JoinDate { get; set; } = DateTime.Today;

    public bool IsActive { get; set; } = true;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<LoyaltyTransaction> Transactions { get; set; } = new();
}

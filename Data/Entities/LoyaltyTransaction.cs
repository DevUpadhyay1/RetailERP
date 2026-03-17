using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 6: Earn/Redeem ledger entry for loyalty points.
/// Type: "Earn" or "Redeem".
/// </summary>
public class LoyaltyTransaction : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid LoyaltyTransactionId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid LoyaltyCardId { get; set; }
    public LoyaltyCard? LoyaltyCard { get; set; }

    [Required, StringLength(10)]
    public string Type { get; set; } = "Earn"; // Earn / Redeem

    [Precision(18, 2)]
    public decimal Points { get; set; }

    // Link to bill that triggered this transaction
    public Guid? PosBillId { get; set; }
    public PosBill? PosBill { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

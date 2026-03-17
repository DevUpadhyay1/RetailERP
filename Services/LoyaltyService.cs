using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Phase 6: Loyalty card + points earn/redeem.
/// Earn rate: 1 point per ₹100 spent (floor).
/// Redeem: 1 point = ₹1 discount, minimum 50 points.
/// Tier thresholds (lifetime points): Bronze 0, Silver 500, Gold 2000, Platinum 5000.
/// </summary>
public class LoyaltyService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    // Configuration constants (move to appsettings if needed)
    public const decimal EarnRatePer100 = 1m;       // 1 point per ₹100
    public const decimal RedeemValuePerPoint = 1m;   // 1 point = ₹1
    public const decimal MinRedeemPoints = 50m;

    public static readonly (byte Tier, string Name, decimal MinPoints)[] TierThresholds =
    {
        (4, "Platinum", 5000m),
        (3, "Gold", 2000m),
        (2, "Silver", 500m),
        (1, "Bronze", 0m)
    };

    public LoyaltyService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Create a loyalty card for a customer.</summary>
    public async Task<LoyaltyCard> CreateCardAsync(Guid customerId)
    {
        var existing = await _db.LoyaltyCards.AnyAsync(c => c.CustomerId == customerId);
        if (existing) throw new InvalidOperationException("Customer already has a loyalty card.");

        var card = new LoyaltyCard
        {
            LoyaltyCardId = Guid.NewGuid(),
            CustomerId = customerId,
            CardNumber = await GenerateCardNumberAsync(),
            PointsBalance = 0,
            LifetimePoints = 0,
            Tier = 1,
            JoinDate = DateTime.Today,
            IsActive = true
        };

        _db.LoyaltyCards.Add(card);
        await _db.SaveChangesAsync();
        return card;
    }

    /// <summary>Lookup loyalty card by customer mobile (Phone) or card number.</summary>
    public async Task<LoyaltyCard?> LookupAsync(string code)
    {
        code = code.Trim();
        return await _db.LoyaltyCards
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c =>
                c.CardNumber == code ||
                (c.Customer != null && c.Customer.Phone == code));
    }

    /// <summary>Earn points when a bill is completed.</summary>
    public async Task<decimal> EarnPointsAsync(Guid loyaltyCardId, Guid posBillId, decimal billAmount)
    {
        var card = await _db.LoyaltyCards.FirstOrDefaultAsync(c => c.LoyaltyCardId == loyaltyCardId)
            ?? throw new InvalidOperationException("Loyalty card not found.");

        if (!card.IsActive) throw new InvalidOperationException("Loyalty card is inactive.");

        var earned = Math.Floor(billAmount / 100m) * EarnRatePer100;
        if (earned <= 0) return 0;

        card.PointsBalance += earned;
        card.LifetimePoints += earned;
        card.Tier = CalculateTier(card.LifetimePoints);

        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyTransactionId = Guid.NewGuid(),
            LoyaltyCardId = loyaltyCardId,
            Type = "Earn",
            Points = earned,
            PosBillId = posBillId,
            Description = $"Earned on bill total ₹{billAmount:N2}",
            OccurredAtUtc = DateTime.UtcNow,
            CompanyId = card.CompanyId
        });

        await _db.SaveChangesAsync();
        return earned;
    }

    /// <summary>Redeem points as bill discount. Returns discount amount in ₹.</summary>
    public async Task<decimal> RedeemPointsAsync(Guid loyaltyCardId, Guid posBillId, decimal pointsToRedeem)
    {
        var card = await _db.LoyaltyCards.FirstOrDefaultAsync(c => c.LoyaltyCardId == loyaltyCardId)
            ?? throw new InvalidOperationException("Loyalty card not found.");

        if (!card.IsActive) throw new InvalidOperationException("Loyalty card is inactive.");
        if (pointsToRedeem < MinRedeemPoints) throw new InvalidOperationException($"Minimum {MinRedeemPoints} points to redeem.");
        if (pointsToRedeem > card.PointsBalance) throw new InvalidOperationException($"Insufficient points. Balance: {card.PointsBalance}");

        card.PointsBalance -= pointsToRedeem;
        var discount = pointsToRedeem * RedeemValuePerPoint;

        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyTransactionId = Guid.NewGuid(),
            LoyaltyCardId = loyaltyCardId,
            Type = "Redeem",
            Points = pointsToRedeem,
            PosBillId = posBillId,
            Description = $"Redeemed {pointsToRedeem} pts → ₹{discount:N2} discount",
            OccurredAtUtc = DateTime.UtcNow,
            CompanyId = card.CompanyId
        });

        await _db.SaveChangesAsync();
        return discount;
    }

    /// <summary>Get tier name string.</summary>
    public static string GetTierName(byte tier) => tier switch
    {
        4 => "Platinum",
        3 => "Gold",
        2 => "Silver",
        _ => "Bronze"
    };

    public static byte CalculateTier(decimal lifetimePoints)
    {
        foreach (var (tier, _, min) in TierThresholds)
        {
            if (lifetimePoints >= min) return tier;
        }
        return 1;
    }

    private async Task<string> GenerateCardNumberAsync()
    {
        var prefix = "LYL-";
        var last = await _db.LoyaltyCards
            .Where(c => c.CardNumber.StartsWith(prefix))
            .OrderByDescending(c => c.CardNumber)
            .Select(c => c.CardNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var numPart = last.Replace(prefix, "");
            if (int.TryParse(numPart, out var n)) next = n + 1;
        }
        return $"{prefix}{next:000000}";
    }
}

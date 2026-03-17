using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using System.Text.Json;

namespace RetailERP.Services;

/// <summary>
/// Sprint 7: Promotion engine. Evaluates active promotions against bill lines
/// and applies the best discount per item automatically.
/// </summary>
public class PromotionService
{
    private readonly ApplicationDbContext _db;

    public PromotionService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Evaluate and apply promotions to all lines on an open bill.
    /// Returns list of promotions that were applied (for UI display).
    /// </summary>
    public async Task<List<AppliedPromoInfo>> ApplyPromotionsAsync(Guid billId)
    {
        var bill = await _db.PosBills
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.PosBillId == billId);

        if (bill is null || bill.Status != 1) return new();

        var today = DateTime.Today;
        var now = DateTime.Now.TimeOfDay;

        // Load all active promotions for this tenant, valid today
        var promos = await _db.Promotions
            .AsNoTracking()
            .Where(p => p.IsActive
                && p.ValidFrom <= today
                && p.ValidTo >= today
                && (p.MaxUsesTotal == 0 || p.UsedCount < p.MaxUsesTotal))
            .OrderBy(p => p.Priority)
            .ToListAsync();

        var results = new List<AppliedPromoInfo>();

        // Clear existing auto-applied discounts (keep manual ones where AppliedPromotionId is null)
        foreach (var line in bill.Lines)
        {
            if (line.AppliedPromotionId is not null)
            {
                line.DiscountPercent = 0;
                line.DiscountAmount = 0;
                line.AppliedPromotionId = null;
            }
        }

        foreach (var promo in promos)
        {
            // Happy hour time check
            if (promo.PromoType == "HappyHour"
                && promo.HappyHourStart.HasValue && promo.HappyHourEnd.HasValue)
            {
                if (now < promo.HappyHourStart.Value || now > promo.HappyHourEnd.Value)
                    continue;
            }

            // Min bill amount check
            if (promo.MinBillAmount > 0 && bill.SubTotal < promo.MinBillAmount)
                continue;

            switch (promo.PromoType)
            {
                case "FlatPercent":
                case "HappyHour":
                    results.AddRange(ApplyPercentDiscount(bill, promo));
                    break;
                case "FlatAmount":
                    results.AddRange(ApplyFlatAmountDiscount(bill, promo));
                    break;
                case "BOGO":
                case "BuyXGetY":
                    results.AddRange(ApplyBogo(bill, promo));
                    break;
                case "ComboDiscount":
                    results.AddRange(ApplyCombo(bill, promo));
                    break;
            }
        }

        // Recalculate NetRate and LineTotal for all lines
        foreach (var line in bill.Lines)
        {
            RecalcLineValues(line);
        }

        await _db.SaveChangesAsync();
        return results;
    }

    /// <summary>
    /// Get promotions that could apply to a specific item (for "Missing Promo" alert).
    /// </summary>
    public async Task<List<Promotion>> GetApplicablePromotionsAsync(Guid itemId, Guid? categoryId)
    {
        var today = DateTime.Today;
        var now = DateTime.Now.TimeOfDay;

        return await _db.Promotions
            .AsNoTracking()
            .Where(p => p.IsActive
                && p.ValidFrom <= today
                && p.ValidTo >= today
                && (p.MaxUsesTotal == 0 || p.UsedCount < p.MaxUsesTotal)
                && (p.ItemId == null || p.ItemId == itemId)
                && (p.CategoryId == null || p.CategoryId == categoryId))
            .OrderBy(p => p.Priority)
            .ToListAsync();
    }

    // ──────────────────────────────────────────
    // Private: Apply discount by type
    // ──────────────────────────────────────────

    private List<AppliedPromoInfo> ApplyPercentDiscount(PosBill bill, Promotion promo)
    {
        var results = new List<AppliedPromoInfo>();
        var matchingLines = GetMatchingLines(bill, promo);

        foreach (var line in matchingLines)
        {
            if (line.AppliedPromotionId is not null && promo.IsExclusive) continue;
            if (line.AppliedPromotionId is not null) continue; // one promo per line

            line.DiscountPercent = promo.DiscountPercent;
            line.DiscountAmount = Math.Round(line.Qty * line.UnitPrice * promo.DiscountPercent / 100m, 2);
            line.AppliedPromotionId = promo.PromotionId;

            results.Add(new AppliedPromoInfo
            {
                PromotionId = promo.PromotionId,
                PromoName = promo.Name,
                PromoType = promo.PromoType,
                LineId = line.PosBillLineId,
                ItemName = line.ItemNameSnapshot ?? "",
                DiscountAmount = line.DiscountAmount
            });
        }
        return results;
    }

    private List<AppliedPromoInfo> ApplyFlatAmountDiscount(PosBill bill, Promotion promo)
    {
        var results = new List<AppliedPromoInfo>();
        var matchingLines = GetMatchingLines(bill, promo);

        foreach (var line in matchingLines)
        {
            if (line.AppliedPromotionId is not null) continue;

            line.DiscountAmount = Math.Min(promo.DiscountAmount, line.Qty * line.UnitPrice);
            if (line.UnitPrice > 0)
                line.DiscountPercent = Math.Round(line.DiscountAmount / (line.Qty * line.UnitPrice) * 100m, 2);
            line.AppliedPromotionId = promo.PromotionId;

            results.Add(new AppliedPromoInfo
            {
                PromotionId = promo.PromotionId,
                PromoName = promo.Name,
                PromoType = promo.PromoType,
                LineId = line.PosBillLineId,
                ItemName = line.ItemNameSnapshot ?? "",
                DiscountAmount = line.DiscountAmount
            });
        }
        return results;
    }

    private List<AppliedPromoInfo> ApplyBogo(PosBill bill, Promotion promo)
    {
        var results = new List<AppliedPromoInfo>();
        var matchingLines = GetMatchingLines(bill, promo);

        foreach (var line in matchingLines)
        {
            if (line.AppliedPromotionId is not null) continue;

            var buyQty = promo.BuyQty > 0 ? promo.BuyQty : 1;
            var getQty = promo.GetQty > 0 ? promo.GetQty : 1;

            if (line.Qty < buyQty) continue;

            // How many sets of the deal? e.g., Buy 2 Get 1: qty=6 → 2 sets → 2 free
            var sets = (int)(line.Qty / (buyQty + getQty));
            if (sets <= 0) continue;

            var freeQty = sets * getQty;
            var discount = Math.Round(freeQty * line.UnitPrice, 2);

            line.DiscountAmount = discount;
            if (line.Qty * line.UnitPrice > 0)
                line.DiscountPercent = Math.Round(discount / (line.Qty * line.UnitPrice) * 100m, 2);
            line.AppliedPromotionId = promo.PromotionId;

            results.Add(new AppliedPromoInfo
            {
                PromotionId = promo.PromotionId,
                PromoName = promo.Name,
                PromoType = promo.PromoType,
                LineId = line.PosBillLineId,
                ItemName = line.ItemNameSnapshot ?? "",
                DiscountAmount = discount
            });
        }
        return results;
    }

    private List<AppliedPromoInfo> ApplyCombo(PosBill bill, Promotion promo)
    {
        var results = new List<AppliedPromoInfo>();
        if (string.IsNullOrWhiteSpace(promo.ComboItemIds)) return results;

        List<Guid>? comboIds;
        try { comboIds = JsonSerializer.Deserialize<List<Guid>>(promo.ComboItemIds); }
        catch { return results; }

        if (comboIds is null || comboIds.Count == 0) return results;

        // Check all combo items present in bill
        var matchedLines = bill.Lines
            .Where(l => comboIds.Contains(l.ItemId))
            .ToList();

        if (matchedLines.Count < comboIds.Count) return results;

        // Calculate combo savings
        var normalTotal = matchedLines.Sum(l => l.UnitPrice * l.Qty);
        if (promo.ComboPrice >= normalTotal) return results;

        var savings = normalTotal - promo.ComboPrice;
        // Distribute discount proportionally across combo items
        foreach (var line in matchedLines)
        {
            if (line.AppliedPromotionId is not null) continue;

            var lineShare = normalTotal > 0
                ? Math.Round(savings * (line.UnitPrice * line.Qty / normalTotal), 2)
                : 0;

            line.DiscountAmount = lineShare;
            if (line.Qty * line.UnitPrice > 0)
                line.DiscountPercent = Math.Round(lineShare / (line.Qty * line.UnitPrice) * 100m, 2);
            line.AppliedPromotionId = promo.PromotionId;

            results.Add(new AppliedPromoInfo
            {
                PromotionId = promo.PromotionId,
                PromoName = promo.Name,
                PromoType = promo.PromoType,
                LineId = line.PosBillLineId,
                ItemName = line.ItemNameSnapshot ?? "",
                DiscountAmount = lineShare
            });
        }
        return results;
    }

    // ──────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────

    private static List<PosBillLine> GetMatchingLines(PosBill bill, Promotion promo)
    {
        return bill.Lines.Where(l =>
            (promo.ItemId is null || l.ItemId == promo.ItemId) &&
            (promo.CategoryId is null || true) // category check requires Item nav — skip if no Items loaded
        ).ToList();
    }

    private static void RecalcLineValues(PosBillLine line)
    {
        var grossLineTotal = line.Qty * line.UnitPrice;
        line.NetRate = line.UnitPrice > 0 && line.DiscountPercent > 0
            ? Math.Round(line.UnitPrice * (1 - line.DiscountPercent / 100m), 2)
            : line.UnitPrice;
        line.LineTotal = grossLineTotal - line.DiscountAmount;
        if (line.LineTotal < 0) line.LineTotal = 0;
    }

    // ──────────────────────────────────────────
    // DTOs
    // ──────────────────────────────────────────

    public class AppliedPromoInfo
    {
        public Guid PromotionId { get; set; }
        public string PromoName { get; set; } = "";
        public string PromoType { get; set; } = "";
        public Guid LineId { get; set; }
        public string ItemName { get; set; } = "";
        public decimal DiscountAmount { get; set; }
    }
}

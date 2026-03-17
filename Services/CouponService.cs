using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Phase 6: Coupon validation and application.
/// </summary>
public class CouponService
{
    private readonly ApplicationDbContext _db;

    public CouponService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Validate and calculate coupon discount for a given bill subtotal.</summary>
    public async Task<CouponResult> ValidateAsync(string code, decimal subTotal)
    {
        var coupon = await _db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code && c.IsActive);

        if (coupon is null)
            return new CouponResult { Success = false, Message = "Coupon not found or inactive." };

        var today = DateTime.Today;
        if (today < coupon.ValidFrom || today > coupon.ValidTo)
            return new CouponResult { Success = false, Message = "Coupon expired or not yet valid." };

        if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
            return new CouponResult { Success = false, Message = "Coupon usage limit reached." };

        if (subTotal < coupon.MinBillAmount)
            return new CouponResult { Success = false, Message = $"Minimum bill amount ₹{coupon.MinBillAmount:N2} required." };

        decimal discount;
        if (coupon.DiscountType == "Percent")
        {
            discount = Math.Round(subTotal * coupon.DiscountValue / 100m, 2);
            if (coupon.MaxDiscount > 0 && discount > coupon.MaxDiscount)
                discount = coupon.MaxDiscount;
        }
        else // Flat
        {
            discount = coupon.DiscountValue;
        }

        // Don't exceed the subtotal
        if (discount > subTotal) discount = subTotal;

        return new CouponResult
        {
            Success = true,
            CouponId = coupon.CouponId,
            Code = coupon.Code,
            Discount = discount,
            Message = $"Coupon applied: ₹{discount:N2} off"
        };
    }

    /// <summary>Record coupon usage when a bill is completed.</summary>
    public async Task RecordUsageAsync(Guid couponId, Guid posBillId, decimal discountApplied)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.CouponId == couponId);
        if (coupon is null) return;

        coupon.UsedCount++;

        _db.CouponUsages.Add(new CouponUsage
        {
            CouponUsageId = Guid.NewGuid(),
            CouponId = couponId,
            PosBillId = posBillId,
            DiscountApplied = discountApplied,
            UsedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public class CouponResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Guid? CouponId { get; set; }
        public string? Code { get; set; }
        public decimal Discount { get; set; }
    }
}

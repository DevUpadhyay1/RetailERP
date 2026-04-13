using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class CouponServiceTests
{
    private ApplicationDbContext GetDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task ValidateAsync_ValidPercentCoupon_ShouldApplyDiscountWithCap()
    {
        using var db = GetDb();
        var couponId = Guid.NewGuid();
        db.Coupons.Add(new Coupon
        {
            CouponId = couponId,
            CompanyId = Guid.NewGuid(),
            Code = "SAVE50",
            DiscountType = "Percent",
            DiscountValue = 50,
            MaxDiscount = 200,
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(10),
            MinBillAmount = 500,
            MaxUses = 10,
            UsedCount = 0,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new CouponService(db);
        
        // 50% of 1000 is 500. Cap is 200. Discount should be 200.
        var result = await service.ValidateAsync("SAVE50", 1000m);

        Assert.True(result.Success);
        Assert.Equal(200m, result.Discount);
    }

    [Fact]
    public async Task ValidateAsync_FlatCoupon_ShouldApplyFlatDiscount()
    {
        using var db = GetDb();
        db.Coupons.Add(new Coupon
        {
            CouponId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Code = "FLAT100",
            DiscountType = "Flat",
            DiscountValue = 100,
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(10),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new CouponService(db);
        
        var result = await service.ValidateAsync("FLAT100", 500m);

        Assert.True(result.Success);
        Assert.Equal(100m, result.Discount);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredCoupon_ShouldReject()
    {
        using var db = GetDb();
        db.Coupons.Add(new Coupon
        {
            CouponId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Code = "EXPIRED",
            ValidFrom = DateTime.Today.AddDays(-10),
            ValidTo = DateTime.Today.AddDays(-1),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new CouponService(db);
        var result = await service.ValidateAsync("EXPIRED", 500m);

        Assert.False(result.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_MaxUsageReached_ShouldReject()
    {
        using var db = GetDb();
        db.Coupons.Add(new Coupon
        {
            CouponId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Code = "LIMITED",
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(10),
            MaxUses = 2,
            UsedCount = 2,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = new CouponService(db);
        var result = await service.ValidateAsync("LIMITED", 500m);

        Assert.False(result.Success);
        Assert.Contains("usage limit reached", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}

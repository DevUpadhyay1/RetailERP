using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class LoyaltyServiceTests
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

    private LoyaltyService GetService(ApplicationDbContext db)
    {
        var audit = new AuditService(db, new HttpContextAccessor());
        return new LoyaltyService(db, audit);
    }

    [Fact]
    public async Task EarnPointsAsync_ShouldCalculateEarnedPointsCorrectly_AndSetTier()
    {
        using var db = GetDb();
        var loyaltyCardId = Guid.NewGuid();
        db.LoyaltyCards.Add(new LoyaltyCard
        {
            LoyaltyCardId = loyaltyCardId,
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CardNumber = "LYL-000001",
            PointsBalance = 0,
            LifetimePoints = 0,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = GetService(db);
        
        // 1 point per 100 spent floor: 1540 / 100 = 15.4 -> 15 points
        var pointsEarned = await service.EarnPointsAsync(loyaltyCardId, Guid.NewGuid(), 1540.50m);

        Assert.Equal(15m, pointsEarned);

        var card = await db.LoyaltyCards.FindAsync(loyaltyCardId);
        Assert.Equal(15m, card!.PointsBalance);
        
        // Initial tier is Bronze (1) when below 500
        Assert.Equal(1, card.Tier);
    }

    [Fact]
    public async Task RedeemPointsAsync_ShouldDeductBalance_WhenSufficientPoints()
    {
        using var db = GetDb();
        var loyaltyCardId = Guid.NewGuid();
        db.LoyaltyCards.Add(new LoyaltyCard
        {
            LoyaltyCardId = loyaltyCardId,
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CardNumber = "LYL-000002",
            PointsBalance = 200,
            LifetimePoints = 200,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = GetService(db);
        
        // Redeem 100 points
        var discountAmount = await service.RedeemPointsAsync(loyaltyCardId, Guid.NewGuid(), 100m);

        Assert.Equal(100m, discountAmount); // 1 pt = 1 rupee usually

        var card = await db.LoyaltyCards.FindAsync(loyaltyCardId);
        Assert.Equal(100m, card!.PointsBalance);
    }

    [Fact]
    public async Task RedeemPointsAsync_ShouldThrow_WhenBelowMinRedeemLimit()
    {
        using var db = GetDb();
        var loyaltyCardId = Guid.NewGuid();
        db.LoyaltyCards.Add(new LoyaltyCard
        {
            LoyaltyCardId = loyaltyCardId,
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CardNumber = "LYL-000003",
            PointsBalance = 40,
            LifetimePoints = 40,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = GetService(db);
        
        // Try redeeming 40, but min is 50
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.RedeemPointsAsync(loyaltyCardId, Guid.NewGuid(), 40m));

        Assert.Contains("Minimum", ex.Message);
    }

    [Fact]
    public async Task RedeemPointsAsync_ShouldThrow_WhenInsufficientBalance()
    {
        using var db = GetDb();
        var loyaltyCardId = Guid.NewGuid();
        db.LoyaltyCards.Add(new LoyaltyCard
        {
            LoyaltyCardId = loyaltyCardId,
            CompanyId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CardNumber = "LYL-000004",
            PointsBalance = 100,
            LifetimePoints = 100,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = GetService(db);
        
        // Try redeeming 150
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.RedeemPointsAsync(loyaltyCardId, Guid.NewGuid(), 150m));

        Assert.Contains("Insufficient points", ex.Message);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class PromotionServiceTests
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
    public async Task ApplyPromotionsAsync_FlatAmount_ShouldCapAtLineTotal()
    {
        using var db = GetDb();
        var itemId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        db.PosBills.Add(new PosBill
        {
            PosBillId = billId,
            CompanyId = Guid.NewGuid(),
            Status = 1, // Open
            SubTotal = 150m,
            Lines = new List<PosBillLine>
            {
                new PosBillLine
                {
                    PosBillLineId = Guid.NewGuid(),
                    ItemId = itemId,
                    Qty = 1,
                    UnitPrice = 150m,
                    NetRate = 150m,
                    LineTotal = 150m
                }
            }
        });

        db.Promotions.Add(new Promotion
        {
            PromotionId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Name = "Flat 200 off",
            PromoType = "FlatAmount",
            DiscountAmount = 200m,   // More than item price
            IsActive = true,
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(1),
            ItemId = itemId
        });
        await db.SaveChangesAsync();

        var service = new PromotionService(db);
        var applied = await service.ApplyPromotionsAsync(billId);

        Assert.Single(applied);
        Assert.Equal(150m, applied[0].DiscountAmount); // Capped at item price (150)
        
        var updatedBill = await db.PosBills.Include(b => b.Lines).FirstAsync();
        Assert.Equal(150m, updatedBill.Lines.First().DiscountAmount);
        Assert.Equal(0m, updatedBill.Lines.First().LineTotal);
    }

    [Fact]
    public async Task ApplyPromotionsAsync_PercentAmount_ShouldCalculateAccurately()
    {
        using var db = GetDb();
        var itemId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        db.PosBills.Add(new PosBill
        {
            PosBillId = billId,
            CompanyId = Guid.NewGuid(),
            Status = 1, // Open
            SubTotal = 500m,
            Lines = new List<PosBillLine>
            {
                new PosBillLine
                {
                    PosBillLineId = Guid.NewGuid(),
                    ItemId = itemId,
                    Qty = 2,
                    UnitPrice = 250m, // Gross line total 500
                    NetRate = 250m,
                    LineTotal = 500m
                }
            }
        });

        db.Promotions.Add(new Promotion
        {
            PromotionId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Name = "20% off",
            PromoType = "FlatPercent",
            DiscountPercent = 20m, 
            IsActive = true,
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(1),
            ItemId = itemId
        });
        await db.SaveChangesAsync();

        var service = new PromotionService(db);
        var applied = await service.ApplyPromotionsAsync(billId);

        Assert.Single(applied);
        // 20% of 500 = 100
        Assert.Equal(100m, applied[0].DiscountAmount);
        
        var updatedBill = await db.PosBills.Include(b => b.Lines).FirstAsync();
        Assert.Equal(100m, updatedBill.Lines.First().DiscountAmount);
        Assert.Equal(400m, updatedBill.Lines.First().LineTotal);
        Assert.Equal(200m, updatedBill.Lines.First().NetRate); // 250 - 20%
    }
}

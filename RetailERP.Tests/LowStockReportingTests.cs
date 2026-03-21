using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class LowStockReportingTests
{
    [Fact]
    public async Task Query_ShouldIncludeItem_WithReorderSet_ButNoStockRows_OnHandCountsAsZero()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company { CompanyId = companyId, Code = "C", Name = "Co" });
        db.Items.Add(new Item
        {
            ItemId = Guid.NewGuid(),
            SKU = "NO-STOCK-ROW",
            Name = "Never stocked",
            UnitPrice = 1,
            ReorderLevel = 10,
            IsActive = true,
            CompanyId = companyId
        });
        await db.SaveChangesAsync();

        var rows = await LowStockReporting.Query(db).ToListAsync();

        Assert.Single(rows);
        Assert.Equal(0, rows[0].OnHand);
        Assert.Equal(10, rows[0].ReorderLevel);
    }

    [Fact]
    public async Task Count_ShouldMatch_WhenOnHandEqualsReorder()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();
        db.Companies.Add(new Company { CompanyId = companyId, Code = "C", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S", Name = "S", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = whId, Name = "W", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "AT-REORDER",
            Name = "At reorder",
            UnitPrice = 1,
            ReorderLevel = 5,
            IsActive = true,
            CompanyId = companyId
        });
        db.Stocks.Add(new Stock
        {
            ItemId = itemId,
            WarehouseId = whId,
            Quantity = 5,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var count = await LowStockReporting.CountAsync(db);
        Assert.Equal(1, count);
    }
}

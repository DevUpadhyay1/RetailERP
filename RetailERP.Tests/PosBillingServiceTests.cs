using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Hubs;
using RetailERP.Services;

namespace RetailERP.Tests;

public class PosBillingServiceTests
{
    [Fact]
    public async Task CompleteBillAsync_ShouldDeductStockAndWriteLedger_WhenBillIsPaid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        db.Companies.Add(new Company
        {
            CompanyId = companyId,
            Code = "TST",
            Name = "Test Company"
        });

        db.Stores.Add(new Store
        {
            StoreId = storeId,
            StoreCode = "S01",
            Name = "Main Store",
            CompanyId = companyId
        });

        db.Warehouses.Add(new Warehouse
        {
            WarehouseId = warehouseId,
            Name = "Main Warehouse",
            StoreId = storeId,
            CompanyId = companyId
        });

        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "ITM-001",
            Name = "Test Item",
            UnitPrice = 100,
            CompanyId = companyId
        });

        db.Stocks.Add(new Stock
        {
            ItemId = itemId,
            WarehouseId = warehouseId,
            Quantity = 50,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.PosBills.Add(new PosBill
        {
            PosBillId = billId,
            BillNo = "POS-TEST-001",
            StoreId = storeId,
            WarehouseId = warehouseId,
            CompanyId = companyId,
            BillDate = DateTime.Today,
            Status = 1,
            SubTotal = 500,
            GrandTotal = 500,
            Lines = new List<PosBillLine>
            {
                new()
                {
                    ItemId = itemId,
                    Qty = 5,
                    UnitPrice = 100,
                    ItemNameSnapshot = "Test Item",
                    LineTotal = 500
                }
            },
            Payments = new List<Payment>
            {
                new()
                {
                    Method = "UPI",
                    Amount = 500,
                    IsRefund = false
                }
            }
        });

        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        var clients = new Mock<IHubClients>();
        var groupClient = new Mock<IClientProxy>();
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupClient.Object);
        hub.Setup(x => x.Clients).Returns(clients.Object);

        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        await sut.CompleteBillAsync(billId);

        var updatedBill = await db.PosBills.FirstAsync(x => x.PosBillId == billId);
        Assert.Equal((byte)2, updatedBill.Status);
        Assert.NotNull(updatedBill.CompletedAtUtc);

        var updatedStock = await db.Stocks.FirstAsync(x => x.ItemId == itemId && x.WarehouseId == warehouseId);
        Assert.Equal(45, updatedStock.Quantity);

        var txn = await db.StockTransactions.FirstOrDefaultAsync(x =>
            x.Type == "OUT" &&
            x.RefType == "PosBill" &&
            x.RefId == billId.ToString() &&
            x.ItemId == itemId);

        Assert.NotNull(txn);
        Assert.Equal(-5, txn!.Qty);
    }

    [Fact]
    public async Task LookupItemAsync_ShouldFindItem_ByBarcode()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "SKU-99",
            Name = "Barcode Product",
            Barcode = "8901234567890",
            UnitPrice = 25,
            CompanyId = companyId
        });
        db.Stocks.Add(new Stock
        {
            ItemId = itemId,
            WarehouseId = warehouseId,
            Quantity = 10,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        var clients = new Mock<IHubClients>();
        var groupClient = new Mock<IClientProxy>();
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupClient.Object);
        hub.Setup(x => x.Clients).Returns(clients.Object);

        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var result = await sut.LookupItemAsync("8901234567890", warehouseId);

        Assert.NotNull(result);
        Assert.Equal(itemId, result!.ItemId);
        Assert.Equal(10, result.StockAvailable);
    }

    [Fact]
    public async Task LookupItemAsync_ShouldFindItem_BySku_WhenBarcodeNull()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "MANUAL-SKU",
            Name = "No Barcode Item",
            Barcode = null,
            UnitPrice = 15,
            CompanyId = companyId
        });
        db.Stocks.Add(new Stock
        {
            ItemId = itemId,
            WarehouseId = warehouseId,
            Quantity = 3,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        var clients = new Mock<IHubClients>();
        var groupClient = new Mock<IClientProxy>();
        clients.Setup(x => x.Group(It.IsAny<string>())).Returns(groupClient.Object);
        hub.Setup(x => x.Clients).Returns(clients.Object);

        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var result = await sut.LookupItemAsync("MANUAL-SKU", warehouseId);

        Assert.NotNull(result);
        Assert.Equal(itemId, result!.ItemId);
    }

    [Fact]
    public async Task LookupItemAsync_ShouldReturnNull_WhenCodeUnknown()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);

        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var result = await sut.LookupItemAsync("DOES-NOT-EXIST", Guid.NewGuid());

        Assert.Null(result);
    }
}

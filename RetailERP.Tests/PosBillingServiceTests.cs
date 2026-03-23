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

    [Fact]
    public async Task CancelBillAsync_ShouldSetStatusCancelled_WhenBillOpen()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.CancelBillAsync(billId);

        var bill = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal((byte)3, bill.Status);
    }

    [Fact]
    public async Task AddLineAsync_ShouldIncrementQty_WhenSameItemScannedTwice()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "SKU-DUP",
            Name = "Dup Item",
            UnitPrice = 50,
            MRP = 60,
            CompanyId = companyId
        });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);
        await sut.AddLineAsync(billId, itemId, 1);

        var lines = await db.PosBillLines.Where(l => l.PosBillId == billId).ToListAsync();
        Assert.Single(lines);
        Assert.Equal(2, lines[0].Qty);
        Assert.Equal(120, lines[0].LineTotal); // 2 * unitPrice from MRP 60
    }

    [Fact]
    public async Task ApplyCouponAsync_ShouldThrow_WhenCouponIsInvalid()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-COUPON", Name = "Coupon Item", UnitPrice = 100, CompanyId = companyId });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ApplyCouponAsync(billId, "BAD-CODE"));
        Assert.Contains("Coupon", ex.Message);
    }

    [Fact]
    public async Task CompleteBillAsync_ShouldThrow_WhenStockIsInsufficient()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-LOW", Name = "Low Stock Item", UnitPrice = 100, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 1, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 2); // more than available stock
        await sut.AddPaymentAsync(billId, "Cash", 500, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteBillAsync(billId));
        Assert.Contains("Insufficient stock", ex.Message);
    }

    [Fact]
    public async Task HoldUnholdComplete_ShouldTransitionStatusesCorrectly()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-HOLD", Name = "Hold Item", UnitPrice = 100, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 10, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.HoldBillAsync(billId);
        var held = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal((byte)4, held.Status);

        await sut.UnholdBillAsync(billId);
        await sut.AddLineAsync(billId, itemId, 1);
        await sut.AddPaymentAsync(billId, "Cash", 100, null);
        await sut.CompleteBillAsync(billId);

        var completed = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal((byte)2, completed.Status);
        Assert.NotNull(completed.CompletedAtUtc);
    }

    [Fact]
    public async Task SetAddDiscountAsync_ShouldClearDiscountAmount_WhenSetToZero()
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

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-DISC", Name = "Disc Item", UnitPrice = 100, CompanyId = companyId });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 2); // subtotal 200
        await sut.SetAddDiscountAsync(billId, 20);

        var withDiscount = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal(20, withDiscount.AddDiscountPercent);
        Assert.True(withDiscount.AddDiscountAmount > 0);

        await sut.SetAddDiscountAsync(billId, 0);
        var cleared = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal(0, cleared.AddDiscountPercent);
        Assert.Equal(0, cleared.AddDiscountAmount);
    }
}

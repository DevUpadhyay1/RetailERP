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
    public async Task RemoveCouponAsync_ShouldClearCouponAndRecalcTotals()
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
        var couponId = Guid.NewGuid();

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RMCP", Name = "Coupon Line Item", UnitPrice = 100, CompanyId = companyId });
        db.Coupons.Add(new Coupon
        {
            CouponId = couponId,
            Code = "RMCP10",
            DiscountType = "Percent",
            DiscountValue = 10,
            MinBillAmount = 0,
            ValidFrom = DateTime.Today.AddDays(-1),
            ValidTo = DateTime.Today.AddDays(1),
            IsActive = true,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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
        await sut.ApplyCouponAsync(billId, "RMCP10");

        var withCoupon = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal(couponId, withCoupon.CouponId);
        Assert.Equal(10, withCoupon.CouponDiscount);
        Assert.Equal(90, withCoupon.GrandTotal);

        await sut.RemoveCouponAsync(billId);
        var cleared = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Null(cleared.CouponId);
        Assert.Equal(0, cleared.CouponDiscount);
        Assert.Equal(100, cleared.GrandTotal);
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

    [Fact]
    public async Task RedeemLoyaltyOnBillAsync_ShouldThrow_WhenNoLoyaltyCardAttached()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-LY", Name = "Loyalty Item", UnitPrice = 100, CompanyId = companyId });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RedeemLoyaltyOnBillAsync(billId, 50));
        Assert.Contains("loyalty card", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveLoyaltyCardAsync_ShouldClearRedemptionAndRecalcTotals()
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
        var customerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        db.Companies.Add(new Company { CompanyId = companyId, Code = "C1", Name = "Co" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "S1", Name = "St", CompanyId = companyId });
        db.Warehouses.Add(new Warehouse { WarehouseId = warehouseId, Name = "Wh", StoreId = storeId, CompanyId = companyId });
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-LYRM", Name = "Item", UnitPrice = 200, CompanyId = companyId });
        db.Customers.Add(new Customer { CustomerId = customerId, Name = "Loyal Cust", CompanyId = companyId });
        db.LoyaltyCards.Add(new LoyaltyCard
        {
            LoyaltyCardId = cardId,
            CustomerId = customerId,
            CardNumber = "LYL-000099",
            PointsBalance = 200,
            LifetimePoints = 200,
            Tier = 1,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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
        await sut.AttachLoyaltyCardAsync(billId, cardId);
        await sut.RedeemLoyaltyOnBillAsync(billId, 50);

        var withLoyalty = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Equal(cardId, withLoyalty.LoyaltyCardId);
        Assert.Equal(50, withLoyalty.LoyaltyPointsRedeemed);
        Assert.Equal(50, withLoyalty.LoyaltyDiscount);
        Assert.Equal(150, withLoyalty.GrandTotal);

        await sut.RemoveLoyaltyCardAsync(billId);
        var cleared = await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId);
        Assert.Null(cleared.LoyaltyCardId);
        Assert.Equal(0, cleared.LoyaltyPointsRedeemed);
        Assert.Equal(0, cleared.LoyaltyDiscount);
        Assert.Equal(200, cleared.GrandTotal);
    }

    [Fact]
    public async Task RemovePaymentAsync_ShouldCauseShortfall_OnComplete_WhenFullPaymentRemoved()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-PAY", Name = "Pay Item", UnitPrice = 100, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 5, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);
        var payment = await sut.AddPaymentAsync(billId, "Cash", 100, null);
        await sut.RemovePaymentAsync(payment.PaymentId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteBillAsync(billId));
        Assert.Contains("Payment shortfall", ex.Message);
    }

    [Fact]
    public async Task ProcessReturnAsync_ShouldRestoreStock_AndAddRefundPayment()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RET", Name = "Return Item", UnitPrice = 50, CompanyId = companyId });
        db.Stocks.Add(new Stock
        {
            ItemId = itemId,
            WarehouseId = warehouseId,
            Quantity = 10,
            CompanyId = companyId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 2);
        var grand = (await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId)).GrandTotal;
        await sut.AddPaymentAsync(billId, "Cash", grand, null);
        await sut.CompleteBillAsync(billId);

        var stockAfterSale = await db.Stocks.FirstAsync(s => s.ItemId == itemId && s.WarehouseId == warehouseId);
        Assert.Equal(8, stockAfterSale.Quantity);

        var billLineId = await db.PosBillLines.Where(l => l.PosBillId == billId).Select(l => l.PosBillLineId).FirstAsync();

        var returnId = await sut.ProcessReturnAsync(billId,
            new List<PosBillingService.ReturnLineInput>
            {
                new() { OriginalBillLineId = billLineId, Qty = 1 }
            },
            reason: "Customer return",
            refundMethod: "Cash",
            processorUserId: null);

        var stockAfterReturn = await db.Stocks.FirstAsync(s => s.ItemId == itemId && s.WarehouseId == warehouseId);
        Assert.Equal(9, stockAfterReturn.Quantity);

        var posReturn = await db.PosReturns.AsNoTracking().FirstAsync(r => r.PosReturnId == returnId);
        Assert.Equal(50, posReturn.TotalRefund);

        var refundPay = await db.Payments.AsNoTracking()
            .SingleAsync(p => p.PosBillId == billId && p.IsRefund && p.PosReturnId == returnId);
        Assert.Equal(50, refundPay.Amount);
        Assert.Equal("Cash", refundPay.Method);
    }

    [Fact]
    public async Task RemovePaymentAsync_ShouldThrow_WhenBillAlreadyCompleted()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RMPAY", Name = "Paid Item", UnitPrice = 100, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 5, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);
        var payment = await sut.AddPaymentAsync(billId, "Cash", 100, null);
        await sut.CompleteBillAsync(billId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RemovePaymentAsync(payment.PaymentId));
        Assert.Contains("open", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessReturnAsync_ShouldReject_WhenReturnExceedsRemainingQtyAcrossMultipleReturns()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-MRET", Name = "Multi Return Item", UnitPrice = 50, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 10, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 2);
        var grand = (await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId)).GrandTotal;
        await sut.AddPaymentAsync(billId, "Cash", grand, null);
        await sut.CompleteBillAsync(billId);

        var billLineId = await db.PosBillLines.Where(l => l.PosBillId == billId).Select(l => l.PosBillLineId).FirstAsync();
        await sut.ProcessReturnAsync(
            billId,
            new List<PosBillingService.ReturnLineInput> { new() { OriginalBillLineId = billLineId, Qty = 1 } },
            "first partial return",
            "Cash",
            null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessReturnAsync(
            billId,
            new List<PosBillingService.ReturnLineInput> { new() { OriginalBillLineId = billLineId, Qty = 2 } },
            "excess second return",
            "Cash",
            null));
        Assert.Contains("remaining qty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessReturnAsync_ShouldThrow_WhenReturnLinesAreEmpty()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RET-EMPTY", Name = "Return Item", UnitPrice = 50, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 10, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);
        var grand = (await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId)).GrandTotal;
        await sut.AddPaymentAsync(billId, "Cash", grand, null);
        await sut.CompleteBillAsync(billId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProcessReturnAsync(billId, new List<PosBillingService.ReturnLineInput>(), "empty", "Cash", null));
        Assert.Contains("at least one return line", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessReturnAsync_ShouldThrow_WhenReturnQtyIsNonPositive()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RET-ZERO", Name = "Return Item", UnitPrice = 50, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 10, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 1);
        var grand = (await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId)).GrandTotal;
        await sut.AddPaymentAsync(billId, "Cash", grand, null);
        await sut.CompleteBillAsync(billId);
        var billLineId = await db.PosBillLines.Where(l => l.PosBillId == billId).Select(l => l.PosBillLineId).FirstAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessReturnAsync(
            billId,
            new List<PosBillingService.ReturnLineInput> { new() { OriginalBillLineId = billLineId, Qty = 0 } },
            "invalid qty",
            "Cash",
            null));
        Assert.Contains("greater than zero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemovePaymentAsync_ShouldThrow_WhenPaymentIsRefundEntry()
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
        db.Items.Add(new Item { ItemId = itemId, SKU = "SKU-RFND", Name = "Refunded Item", UnitPrice = 50, CompanyId = companyId });
        db.Stocks.Add(new Stock { ItemId = itemId, WarehouseId = warehouseId, Quantity = 10, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var audit = new AuditService(db, new HttpContextAccessor());
        var loyalty = new LoyaltyService(db, audit);
        var coupons = new CouponService(db);
        var hub = new Mock<IHubContext<RetailHub>>();
        hub.Setup(x => x.Clients).Returns(new Mock<IHubClients>().Object);
        var sut = new PosBillingService(db, audit, loyalty, coupons, hub.Object);

        var billId = await sut.CreateBillAsync(storeId, warehouseId, null, null);
        await sut.AddLineAsync(billId, itemId, 2);
        var grand = (await db.PosBills.AsNoTracking().FirstAsync(b => b.PosBillId == billId)).GrandTotal;
        await sut.AddPaymentAsync(billId, "Cash", grand, null);
        await sut.CompleteBillAsync(billId);
        var billLineId = await db.PosBillLines.Where(l => l.PosBillId == billId).Select(l => l.PosBillLineId).FirstAsync();
        var returnId = await sut.ProcessReturnAsync(
            billId,
            new List<PosBillingService.ReturnLineInput> { new() { OriginalBillLineId = billLineId, Qty = 1 } },
            "customer return",
            "Cash",
            null);
        var refundPaymentId = await db.Payments.Where(p => p.PosReturnId == returnId).Select(p => p.PaymentId).FirstAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RemovePaymentAsync(refundPaymentId));
        Assert.Contains("refund", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

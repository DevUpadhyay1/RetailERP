using System.Text;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class ItemOnboardingServiceTests
{
    [Fact]
    public void BuildStandardTemplateCsv_ShouldContainExpectedHeaders()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        var svc = new ItemOnboardingService(db);

        var csv = Encoding.UTF8.GetString(svc.BuildStandardTemplateCsv());

        Assert.Contains("SKU,Name,UnitPrice,MRP,PurchasePrice,GstPercent,HsnCode,Barcode,ReorderLevel,UnitName,CategoryName,IsActive", csv);
        Assert.Contains("OpeningStock,WarehouseName,BatchNumber,ExpiryDate", csv);
        Assert.Contains("RICE-001", csv);
    }

    [Fact]
    public async Task ImportCsvAsync_ShouldInsertItemsAndCreateLookups()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        var svc = new ItemOnboardingService(db);

        var csv = string.Join(Environment.NewLine, new[]
        {
            "SKU,Name,UnitPrice,MRP,PurchasePrice,GstPercent,HsnCode,Barcode,ReorderLevel,UnitName,CategoryName,IsActive",
            "TEST-001,Test Item 1,120,140,95,5,1234,8900000000001,10,PCS,Grocery,true",
            "TEST-002,Test Item 2,220,240,170,18,5678,8900000000002,5,BOX,Hardware,true"
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportCsvAsync(stream, "unit-test.csv", updateExisting: true, createMissingLookups: true);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);

        Assert.Equal(2, db.Items.Count());
        Assert.Contains(db.Units, u => u.Name == "PCS");
        Assert.Contains(db.Units, u => u.Name == "BOX");
        Assert.Contains(db.Categories, c => c.Name == "Grocery");
        Assert.Contains(db.Categories, c => c.Name == "Hardware");
    }

    [Fact]
    public async Task ImportCsvAsync_ShouldFlagDuplicateSkuInSameFile()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        var svc = new ItemOnboardingService(db);

        var csv = string.Join(Environment.NewLine, new[]
        {
            "SKU,Name,UnitPrice,MRP,PurchasePrice,GstPercent,HsnCode,Barcode,ReorderLevel,UnitName,CategoryName,IsActive",
            "DUP-001,Item One,100,110,80,5,1111,8900000001001,3,PCS,Grocery,true",
            "DUP-001,Item One Duplicate,100,110,80,5,1111,8900000001002,3,PCS,Grocery,true"
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportCsvAsync(stream, "dup.csv", updateExisting: true, createMissingLookups: true);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate SKU in file"));
    }

    [Fact]
    public async Task ImportCsvAsync_WithOpeningStock_ShouldCreateStockAndOpeningTransaction()
    {
        using var db = TestDbFactory.CreateInMemoryDb();

        var warehouseId = Guid.NewGuid();
        db.Warehouses.Add(new Warehouse
        {
            WarehouseId = warehouseId,
            Name = "Main Warehouse"
        });
        await db.SaveChangesAsync();

        var svc = new ItemOnboardingService(db);
        var csv = string.Join(Environment.NewLine, new[]
        {
            "SKU,Name,UnitPrice,MRP,PurchasePrice,GstPercent,HsnCode,Barcode,ReorderLevel,UnitName,CategoryName,IsActive,OpeningStock,WarehouseName,BatchNumber,ExpiryDate",
            "OPEN-001,Opening Item,100,120,80,5,1234,8900000001234,10,PCS,Grocery,true,25,Main Warehouse,BATCH-A,2027-12-31"
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportCsvAsync(stream, "opening.csv", updateExisting: true, createMissingLookups: true);

        Assert.Equal(1, result.Inserted);
        Assert.Empty(result.Errors);

        var item = Assert.Single(db.Items);
        var stock = Assert.Single(db.Stocks);
        Assert.Equal(item.ItemId, stock.ItemId);
        Assert.Equal(warehouseId, stock.WarehouseId);
        Assert.Equal(25m, stock.Quantity);
        Assert.Equal("BATCH-A", stock.BatchNumber);
        Assert.Equal(new DateTime(2027, 12, 31), stock.ExpiryDate);

        var tx = Assert.Single(db.StockTransactions);
        Assert.Equal("OPENING", tx.Type);
        Assert.Equal(25m, tx.Qty);
        Assert.Equal("ItemOnboarding", tx.RefType);
    }
}

using System.Text;
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
}

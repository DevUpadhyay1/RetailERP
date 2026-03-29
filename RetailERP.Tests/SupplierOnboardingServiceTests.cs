using System.Text;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class SupplierOnboardingServiceTests
{
    [Fact]
    public void BuildTemplateCsv_ShouldContainExpectedHeaders()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        var svc = new SupplierOnboardingService(db);

        var csv = Encoding.UTF8.GetString(svc.BuildTemplateCsv());

        Assert.Contains("Name,ContactPerson,Phone,Email,GSTIN,Address,OpeningBalance", csv);
        Assert.Contains("Prime Distributors", csv);
    }

    [Fact]
    public async Task ImportCsvAsync_ShouldInsertAndUpdateSuppliers()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        db.Suppliers.Add(new Supplier
        {
            SupplierId = Guid.NewGuid(),
            Name = "Prime Traders",
            ContactPerson = "Old Person",
            OpeningBalance = 500m
        });
        await db.SaveChangesAsync();

        var svc = new SupplierOnboardingService(db);
        var csv = string.Join(Environment.NewLine, new[]
        {
            "Name,ContactPerson,Phone,Email,GSTIN,Address,OpeningBalance",
            "Prime Traders,Rahul,9876500001,prime@example.com,24ABCDE1234F1Z5,Updated Address,1500",
            "New Supplier,Anita,9825000000,newsup@example.com,24AABCT1234C1Z8,New Address,0"
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportCsvAsync(stream, "suppliers.csv", updateExisting: true);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Errors);

        var existing = db.Suppliers.Single(c => c.Name == "Prime Traders");
        Assert.Equal("Rahul", existing.ContactPerson);
        Assert.Equal(1500m, existing.OpeningBalance);

        var added = db.Suppliers.Single(c => c.Name == "New Supplier");
        Assert.Equal("Anita", added.ContactPerson);
    }
}

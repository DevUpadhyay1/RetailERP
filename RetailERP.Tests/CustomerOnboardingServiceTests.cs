using System.Text;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class CustomerOnboardingServiceTests
{
    [Fact]
    public void BuildTemplateCsv_ShouldContainExpectedHeaders()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        var svc = new CustomerOnboardingService(db);

        var csv = Encoding.UTF8.GetString(svc.BuildTemplateCsv());

        Assert.Contains("Name,Phone,Email,Address,GSTIN,OpeningBalance", csv);
        Assert.Contains("Walk-in Customer", csv);
    }

    [Fact]
    public async Task ImportCsvAsync_ShouldInsertAndUpdateCustomers()
    {
        using var db = TestDbFactory.CreateInMemoryDb();
        db.Customers.Add(new Customer
        {
            CustomerId = Guid.NewGuid(),
            Name = "Existing Customer",
            Phone = "9876500000",
            OpeningBalance = 10m
        });
        await db.SaveChangesAsync();

        var svc = new CustomerOnboardingService(db);
        var csv = string.Join(Environment.NewLine, new[]
        {
            "Name,Phone,Email,Address,GSTIN,OpeningBalance",
            "Existing Customer,9876500000,existing@example.com,Updated Address,24ABCDE1234F1Z5,250",
            "New Customer,9898989898,new@example.com,New Address,24AABCT1234C1Z8,-100"
        });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await svc.ImportCsvAsync(stream, "customers.csv", updateExisting: true);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Empty(result.Errors);

        var existing = db.Customers.Single(c => c.Name == "Existing Customer");
        Assert.Equal("existing@example.com", existing.Email);
        Assert.Equal(250m, existing.OpeningBalance);

        var added = db.Customers.Single(c => c.Name == "New Customer");
        Assert.Equal(-100m, added.OpeningBalance);
    }
}

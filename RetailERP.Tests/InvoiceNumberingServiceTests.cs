using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class InvoiceNumberingServiceTests
{
    [Fact]
    public async Task GenerateNextInvoiceNoAsync_ShouldCreateDefaultRule_WhenMissing()
    {
        using var db = TestDbFactory.CreateInMemoryDb();

        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.Companies.Add(new Company { CompanyId = companyId, Code = "CMP-01", Name = "Default Company" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "STR-01", Name = "Main Store", CompanyId = companyId });
        await db.SaveChangesAsync();

        var sut = new InvoiceNumberingService(db);
        var date = new DateTime(2026, 4, 4);

        var first = await sut.GenerateNextInvoiceNoAsync(companyId, storeId, InvoiceDocumentType.TaxInvoice, date);
        var second = await sut.GenerateNextInvoiceNoAsync(companyId, storeId, InvoiceDocumentType.TaxInvoice, date);

        Assert.Equal("TAX-2026-0001", first);
        Assert.Equal("TAX-2026-0002", second);

        var rule = Assert.Single(db.InvoiceNumberingRules);
        Assert.Equal(3, rule.NextNumber);
        Assert.Equal(InvoiceNumberResetPolicy.Yearly, rule.ResetPolicy);
    }

    [Fact]
    public async Task GenerateNextInvoiceNoAsync_ShouldUseStoreOverride_WhenAvailable()
    {
        using var db = TestDbFactory.CreateInMemoryDb();

        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();

        db.Companies.Add(new Company { CompanyId = companyId, Code = "CMP-02", Name = "Override Company" });
        db.Stores.Add(new Store { StoreId = storeId, StoreCode = "STR-02", Name = "Branch", CompanyId = companyId });

        db.InvoiceNumberingRules.Add(new InvoiceNumberingRule
        {
            InvoiceNumberingRuleId = Guid.NewGuid(),
            CompanyId = companyId,
            StoreId = null,
            DocumentType = InvoiceDocumentType.TaxInvoice,
            Prefix = "TAX",
            NumberWidth = 4,
            NextNumber = 1,
            ResetPolicy = InvoiceNumberResetPolicy.Yearly,
            IsActive = true
        });

        db.InvoiceNumberingRules.Add(new InvoiceNumberingRule
        {
            InvoiceNumberingRuleId = Guid.NewGuid(),
            CompanyId = companyId,
            StoreId = storeId,
            DocumentType = InvoiceDocumentType.TaxInvoice,
            Prefix = "STR",
            NumberWidth = 5,
            NextNumber = 10,
            ResetPolicy = InvoiceNumberResetPolicy.Never,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var sut = new InvoiceNumberingService(db);
        var date = new DateTime(2026, 4, 4);

        var storeInvoiceNo = await sut.GenerateNextInvoiceNoAsync(companyId, storeId, InvoiceDocumentType.TaxInvoice, date);
        var companyInvoiceNo = await sut.GenerateNextInvoiceNoAsync(companyId, null, InvoiceDocumentType.TaxInvoice, date);

        Assert.Equal("STR-00010", storeInvoiceNo);
        Assert.Equal("TAX-2026-0001", companyInvoiceNo);
    }

    [Fact]
    public async Task GenerateNextInvoiceNoAsync_ShouldResetMonthlyCounter_OnNewMonth()
    {
        using var db = TestDbFactory.CreateInMemoryDb();

        var companyId = Guid.NewGuid();
        db.Companies.Add(new Company { CompanyId = companyId, Code = "CMP-03", Name = "Monthly Company" });

        var ruleId = Guid.NewGuid();
        db.InvoiceNumberingRules.Add(new InvoiceNumberingRule
        {
            InvoiceNumberingRuleId = ruleId,
            CompanyId = companyId,
            StoreId = null,
            DocumentType = InvoiceDocumentType.ProformaInvoice,
            Prefix = "PRO",
            NumberWidth = 3,
            NextNumber = 42,
            ResetPolicy = InvoiceNumberResetPolicy.Monthly,
            LastResetAtUtc = new DateTime(2026, 2, 5),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var sut = new InvoiceNumberingService(db);
        var invoiceNo = await sut.GenerateNextInvoiceNoAsync(
            companyId,
            null,
            InvoiceDocumentType.ProformaInvoice,
            new DateTime(2026, 3, 1));

        Assert.Equal("PRO-202603-001", invoiceNo);

        var refreshed = await db.InvoiceNumberingRules.FindAsync(ruleId);
        Assert.NotNull(refreshed);
        Assert.Equal(2, refreshed!.NextNumber);
        Assert.Equal(new DateTime(2026, 3, 1), refreshed.LastResetAtUtc!.Value.Date);
    }
}

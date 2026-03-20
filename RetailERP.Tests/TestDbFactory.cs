using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Tests;

internal static class TestDbFactory
{
    public static ApplicationDbContext CreateInMemoryDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N"))
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

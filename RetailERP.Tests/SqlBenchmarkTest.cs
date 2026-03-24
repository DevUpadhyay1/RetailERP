using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Controllers.Api;
using Xunit;

namespace RetailERP.Tests;

public class SqlBenchmarkTest
{
    [Fact(Skip = "Manual scale benchmark")]
    public async Task Profile_Real_100k_Items()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RetailERPDb;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;
        
        using var db = new ApplicationDbContext(options);
        var controller = new ItemsController(db);
        
        // Mock User Context so that `GetCompanyId()` doesn't throw NullReferenceException
        var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim("companyId", "00000000-0000-0000-0000-000000000001")
        ], "mock"));
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user }
        };

        // Warm up EF Core and SQL Connection
        await controller.GetAll(null, null, null, 1, 20);
        
        var loops = 10;
        long totalP1 = 0;
        for(int i=0; i<loops; i++)
        {
            var sw = Stopwatch.StartNew();
            await controller.GetAll(null, null, null, 1, 20);
            sw.Stop();
            totalP1 += sw.ElapsedMilliseconds;
        }

        long totalP500 = 0;
        for(int i=0; i<loops; i++)
        {
            var sw2 = Stopwatch.StartNew();
            await controller.GetAll(null, null, null, 500, 20); // deep page
            sw2.Stop();
            totalP500 += sw2.ElapsedMilliseconds;
        }
        
        long totalSearch = 0;
        for(int i=0; i<loops; i++)
        {
            var sw3 = Stopwatch.StartNew();
            await controller.GetAll("BLK-99", null, null, 1, 20); // Search by SKU
            sw3.Stop();
            totalSearch += sw3.ElapsedMilliseconds;
        }

        var result = $@"--- 100K SQL BENCHMARK RESULTS ---
Page 1 Latency: {totalP1 / loops} ms avg
Page 500 Latency (Deep Offset): {totalP500 / loops} ms avg
Search by SKU: {totalSearch / loops} ms avg
----------------------------------";

        System.IO.File.WriteAllText(@"c:\7th_Semester\RetailERP\benchmark_sql_result.txt", result);
        Assert.True(true);
    }
}

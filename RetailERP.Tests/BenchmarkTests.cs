using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RetailERP.Data;
using RetailERP.Data.Entities;
using Xunit.Abstractions;

namespace RetailERP.Tests;

/// <summary>
/// Performance snapshot test to measure the baseline latency of the optimized
/// Api/ItemsController.GetAll endpoint (which uses AsNoTracking and Select projection).
/// </summary>
public class BenchmarkTests : IClassFixture<DevelopmentWebApplicationFactory>
{
    private readonly DevelopmentWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public BenchmarkTests(DevelopmentWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Measure_ItemsGetAll_Latency()
    {
        // 1. Arrange: Create a client that bypasses authentication
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove auth to make testing easier without a JWT
                services.AddControllers(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AllowAnonymousFilter()));
            });
        }).CreateClient();

        // Seed some data into the SQLite In-Memory DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (!db.Items.Any())
            {
                for (int i = 0; i < 50; i++)
                {
                    db.Items.Add(new Item { SKU = $"SKU-{i}", Name = $"Item {i}", UnitPrice = 100, IsActive = true, ReorderLevel = 5 });
                }
                await db.SaveChangesAsync();
            }
        }

        var times = new List<long>();
        var url = "Api/ItemsController.GetAll";

        // 3. Measure 5 requests by directly invoking the controller to bypass HTTP Auth / 429 Rate Limits
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var controller = new RetailERP.Controllers.Api.ItemsController(db);

            // Warm up
            await controller.GetAll(null, null, null, 1, 20);

            for (int i = 0; i < 5; i++)
            {
                var sw = Stopwatch.StartNew();
                var result = await controller.GetAll(null, null, null, 1, 20);
                sw.Stop();
                times.Add(sw.ElapsedMilliseconds);
            }
        }

        var min = times.Min();
        var max = times.Max();
        var avg = times.Average();

        var resultText = $@"--- BENCHMARK RESULTS ---
Endpoint: {url}
Min Latency: {min} ms
Max Latency: {max} ms
Avg Latency: {Math.Round(avg, 2)} ms
-------------------------";
        System.IO.File.WriteAllText(@"c:\7th_Semester\RetailERP\benchmark_result.txt", resultText);
        Assert.True(avg >= 0);
    }
}

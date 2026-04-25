using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Services;

namespace RetailERP.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task SaveLayoutAsync_InvalidatesCachedDefaultAndReturnsSavedLayout()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var initial = await service.GetLayoutAsync(userId, BusinessType.Supermarket, "Admin");
        Assert.NotEmpty(initial.Layout);
        Assert.True(initial.IsDefault);

        var customLayout = new List<WidgetPlacement>
        {
            new("sales-purchases-chart", 0, 0, 6, 4),
            new("top-items-bar", 6, 0, 6, 4),
            new("low-stock-list", 0, 4, 6, 4)
        };

        var savedAtUtc = await service.SaveLayoutAsync(userId, customLayout);
        var reloaded = await service.GetLayoutAsync(userId, BusinessType.Supermarket, "Admin");

        Assert.False(reloaded.IsDefault);
        Assert.NotNull(reloaded.LastModifiedUtc);
        Assert.Equal(savedAtUtc, reloaded.LastModifiedUtc);
        Assert.Equal(customLayout, reloaded.Layout);
    }

    [Fact]
    public async Task ResetLayoutAsync_RemovesPersistedLayoutAndFallsBackToDefault()
    {
        var userId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await service.SaveLayoutAsync(userId, new List<WidgetPlacement>
        {
            new("recent-pos-bills", 0, 0, 6, 4)
        });

        await service.ResetLayoutAsync(userId);

        var reloaded = await service.GetLayoutAsync(userId, BusinessType.Kirana, "Admin");
        Assert.True(reloaded.IsDefault);
        Assert.NotEmpty(reloaded.Layout);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("DashboardServiceTests_" + Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options, tenant: null);
    }

    private static DashboardService CreateService(ApplicationDbContext db)
    {
        var distributedCache = new FakeDistributedCache();
        var cache = new CacheService(distributedCache, new FakeTenantProvider());
        return new DashboardService(db, cache);
    }

    private sealed class FakeTenantProvider : ITenantProvider
    {
        public Guid? CompanyId => Guid.Parse("00000000-0000-0000-0000-000000000001");
        public bool IsSuperAdmin => false;
    }

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public byte[]? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => _store[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}

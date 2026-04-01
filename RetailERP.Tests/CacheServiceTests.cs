using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using RetailERP.Services;

namespace RetailERP.Tests;

public class CacheServiceTests
{
    [Fact]
    public async Task GetOrSetAsync_ShouldUseCompanyScopedKey_WhenTenantCompanyIsPresent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(x => x.IsSuperAdmin).Returns(false);
        tenant.SetupGet(x => x.CompanyId).Returns(companyId);

        var cache = new Mock<IDistributedCache>();
        cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var sut = new CacheService(cache.Object, tenant.Object);

        var value = await sut.GetOrSetAsync("dashboard", () => Task.FromResult(42));

        Assert.Equal(42, value);
        cache.Verify(x => x.GetAsync($"t:{companyId}:dashboard", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(x => x.SetAsync(
            $"t:{companyId}:dashboard",
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_ShouldUseGlobalKey_WhenSuperAdmin()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(x => x.IsSuperAdmin).Returns(true);
        tenant.SetupGet(x => x.CompanyId).Returns((Guid?)null);

        var cache = new Mock<IDistributedCache>();
        cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var sut = new CacheService(cache.Object, tenant.Object);

        var value = await sut.GetOrSetAsync("kpi", () => Task.FromResult("ok"));

        Assert.Equal("ok", value);
        cache.Verify(x => x.GetAsync("t:global:kpi", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrSetAsync_ShouldReturnCachedValue_WithoutCallingFactory_WhenCacheHit()
    {
        var companyId = Guid.NewGuid();
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(x => x.IsSuperAdmin).Returns(false);
        tenant.SetupGet(x => x.CompanyId).Returns(companyId);

        var cache = new Mock<IDistributedCache>();
        cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes("cached-value"));

        var sut = new CacheService(cache.Object, tenant.Object);
        var factoryCalled = false;

        var value = await sut.GetOrSetAsync("cached-key", () =>
        {
            factoryCalled = true;
            return Task.FromResult("new-value");
        });

        Assert.Equal("cached-value", value);
        Assert.False(factoryCalled);
        cache.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_ShouldUseTenantScopedKey()
    {
        var companyId = Guid.NewGuid();
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(x => x.IsSuperAdmin).Returns(false);
        tenant.SetupGet(x => x.CompanyId).Returns(companyId);

        var cache = new Mock<IDistributedCache>();
        var sut = new CacheService(cache.Object, tenant.Object);

        await sut.RemoveAsync("sales-summary");

        cache.Verify(x => x.RemoveAsync($"t:{companyId}:sales-summary", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveByPrefixAsync_ShouldUseTenantScopedPrefixKey()
    {
        var companyId = Guid.NewGuid();
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(x => x.IsSuperAdmin).Returns(false);
        tenant.SetupGet(x => x.CompanyId).Returns(companyId);

        var cache = new Mock<IDistributedCache>();
        var sut = new CacheService(cache.Object, tenant.Object);

        await sut.RemoveByPrefixAsync("dashboard");

        cache.Verify(x => x.RemoveAsync($"t:{companyId}:dashboard", It.IsAny<CancellationToken>()), Times.Once);
    }
}

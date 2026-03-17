using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace RetailERP.Services;

/// <summary>
/// Sprint 4 – Convenience wrapper around IDistributedCache with JSON serialization.
/// Works with both Redis and in-memory distributed cache transparently.
/// </summary>
public sealed class CacheService
{
    private readonly IDistributedCache _cache;
    private readonly ITenantProvider _tenant;

    public CacheService(IDistributedCache cache, ITenantProvider tenant)
    {
        _cache = cache;
        _tenant = tenant;
    }

    /// <summary>Build a tenant-scoped cache key to prevent cross-tenant leakage.</summary>
    private string TenantKey(string key)
    {
        var prefix = _tenant.IsSuperAdmin ? "global" : (_tenant.CompanyId?.ToString() ?? "anon");
        return $"t:{prefix}:{key}";
    }

    /// <summary>Get a cached value, or compute + cache it if missing.</summary>
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
    {
        var cacheKey = TenantKey(key);
        var bytes = await _cache.GetAsync(cacheKey);

        if (bytes is not null)
        {
            return JsonSerializer.Deserialize<T>(bytes)!;
        }

        var value = await factory();

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromMinutes(5),
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(2)
        };

        var serialized = JsonSerializer.SerializeToUtf8Bytes(value);
        await _cache.SetAsync(cacheKey, serialized, options);

        return value;
    }

    /// <summary>Remove a cached key (e.g., after data update).</summary>
    public Task RemoveAsync(string key)
        => _cache.RemoveAsync(TenantKey(key));

    /// <summary>Remove all keys matching a prefix pattern (tenant-scoped).</summary>
    public async Task RemoveByPrefixAsync(string prefix)
    {
        // Note: IDistributedCache doesn't support pattern deletion.
        // For in-memory cache, individual Remove calls are needed.
        // For Redis in production, use StackExchange.Redis SCAN + DEL.
        // For now, remove known keys explicitly when needed.
        await _cache.RemoveAsync(TenantKey(prefix));
    }
}

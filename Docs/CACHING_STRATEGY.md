# Caching strategy (RetailERP)

Short reference for **where caching helps** and **what to avoid** as data and traffic grow.

## Current usage

- **Distributed cache (Redis when configured):** session / multi-instance–safe patterns can use `IDistributedCache` (see `CacheService` and configuration in `WebApplicationBuilderExtensions`).
- **In-memory fallback:** when Redis is not configured, the app uses in-memory distributed cache (single-instance only).

## Hot spots to revisit first

| Area | Risk | Suggestion |
|------|------|------------|
| Dashboard widgets | Repeated aggregations on each load | Cache keyed by `companyId` + widget + short TTL (30–120s); invalidate on rare admin changes if needed. |
| Sales / GST reports | Heavy date-range queries | Read-only `AsNoTracking()`, narrow projections, indexes; optional report cache for “yesterday and older” ranges. |
| POS item lookup | High frequency, low latency | Keep DB indexes (SKU/barcode per tenant); avoid loading full `Item` graphs; consider memory cache for barcode→id per store with short TTL. |
| Reference data (units, categories) | Stable, read-heavy | Safe candidates for longer TTL or app-start warm cache per tenant. |

## Principles

1. **Key by tenant:** include `CompanyId` (or tenant id) in every cache key to prevent cross-tenant leakage.
2. **TTL over manual invalidation** for dashboards unless you have a clear invalidation event.
3. **Do not cache money-final truth:** POS completion and stock writes always go to the database; cache is for reads and hints only.
4. **Prefer query shape fixes first:** `Select` projections and removing N+1 often beat caching for correctness and simplicity.

## Production checklist

- [ ] Redis connection string set when running multiple instances.
- [ ] No user-specific secrets stored in cache values.
- [ ] Monitor cache hit rate and DB load after enabling TTLs on hot endpoints.

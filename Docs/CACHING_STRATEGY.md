# RetailERP Caching & Profiling Strategy

Short reference for where caching helps and what to avoid as data and traffic grow.

## 1. 100k Dataset Profiling
To determine the production necessity of an external distributed cache (Redis), the system was benchmarked against a production-volume catalog payload.
- **Dataset:** 100,000 generated SKU records loaded into the `Items` SQL Server LocalDB table via a transaction-batched script.
- **Methodology:** The measurements were sourced directly from the core `Api/ItemsController.GetAll()` bound to the genuine `ApplicationDbContext`. Each query was iterated to eliminate cold-boot noise.

## 2. Uncached Latency Baseline

| Scenario | Average Latency (ms) | Bottleneck |
|----------|----------------------|------------|
| Page 1 (No search) | **446 ms** | Paging `COUNT(*)` over 100k rows forces an index scan. |
| Page 500 (No search) | **575 ms** | Offset/Fetch performance degradation on deep query positioning. |
| Search by SKU | **1525 ms** | EF Core `Contains` mapping to `LIKE '%BLK-99%'` executes a heavy clustered index table scan. |

## 3. Redis Decision & Architecture
Based on empirical degradation for broad string searches (1.5s+) and the continuous ~500ms baseline tax on catalog rendering, **Redis caching is strictly required for scale, but it must be applied selectively.**

### Decision Matrix: Selective Cache

| Component | Strategy Option | Reason |
|-----------|-----------------|--------|
| **Catalog Base (Page 1)** | CACHE (Short TTL) | High frequency, highly repeatable. 446ms is too slow for snappy POS load times. |
| **Deep Nav (Page 50+)** | DO NOT CACHE | Prevents memory eviction thrashing. Users rarely navigate beyond page 5 manually. |
| **String Search** | DO NOT CACHE | Infinite permutation space (`q=Lapt`, `q=Lapt `) will destroy cache hit-rates. |
| **Dashboard** | CACHE (Keyed by Tenant) | Eliminates repetitive heavy aggregation joins across sales history. |

## 4. Next Action for Search
Because String Searches were isolated as the system's apex bottleneck (`1525ms`), caching is the incorrect solution. 
The next engineering step must be implementing a **Full-Text Indexing** engine (or explicit non-clustered `SQL LIKE` indexing) on `Items.SKU` / `Items.Name` to drop lookup latency below 50ms.

## 5. Implementation Rules
1. **Key by tenant:** Always include `CompanyId` in every cache key to prevent cross-tenant IDOR leakage.
2. **TTL over manual invalidation** for dashboards (30-120s).
3. **Do not cache money-final truth:** POS financial commits and stock decrements MUST ONLY hit SQL Server. Cache is designated exclusively for reading aggregates/hints.

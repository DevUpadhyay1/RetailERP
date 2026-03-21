using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Services;

/// <summary>
/// Single definition of "low stock" used by MVC, API, dashboard widgets, background jobs, and monitoring.
/// <para>
/// Rule: active item, ReorderLevel &gt; 0, and total quantity across all warehouses (sum of <see cref="Data.Entities.Stock"/>)
/// is <b>at or below</b> reorder level. Items with <b>no</b> stock rows count as 0 on-hand (so they appear when reorder is set).
/// </para>
/// </summary>
public static class LowStockReporting
{
    /// <summary>Queryable of all low-stock rows (respects tenant filters on <paramref name="db"/>).</summary>
    public static IQueryable<LowStockRowDto> Query(ApplicationDbContext db) =>
        db.Items.AsNoTracking()
            .Where(i => i.IsActive && i.ReorderLevel > 0)
            .Select(i => new LowStockRowDto
            {
                ItemId = i.ItemId,
                CompanyId = i.CompanyId,
                SKU = i.SKU,
                Name = i.Name,
                OnHand = db.Stocks.Where(s => s.ItemId == i.ItemId).Sum(s => (decimal?)s.Quantity) ?? 0,
                ReorderLevel = i.ReorderLevel
            })
            .Where(x => x.OnHand <= x.ReorderLevel);

    public static Task<int> CountAsync(ApplicationDbContext db, CancellationToken cancellationToken = default) =>
        Query(db).CountAsync(cancellationToken);
}

/// <summary>Shared row shape for low-stock lists (EF-projectable).</summary>
public sealed class LowStockRowDto
{
    public Guid ItemId { get; set; }
    public Guid? CompanyId { get; set; }
    public string SKU { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal OnHand { get; set; }
    public decimal ReorderLevel { get; set; }
}

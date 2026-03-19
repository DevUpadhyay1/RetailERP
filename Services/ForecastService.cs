using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Services;

/// <summary>
/// Sprint 13: Forecasting, auto-reorder suggestions, and sales anomaly detection.
/// Uses historical POS + Invoice demand to compute item-wise projections.
/// </summary>
public sealed class ForecastService
{
    private readonly ApplicationDbContext _db;

    public ForecastService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ForecastRow>> BuildForecastAsync(
        DateTime from,
        DateTime to,
        int horizonDays,
        int leadTimeDays,
        Guid? warehouseId = null)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start) (start, end) = (end, start);

        horizonDays = Math.Clamp(horizonDays, 1, 90);
        leadTimeDays = Math.Clamp(leadTimeDays, 1, 30);

        var snapshot = await LoadSnapshotAsync(start, end, warehouseId);
        if (snapshot.Keys.Count == 0)
            return new List<ForecastRow>();

        var historyDays = (end - start).Days + 1;
        var rows = new List<ForecastRow>(snapshot.Keys.Count);

        foreach (var key in snapshot.Keys)
        {
            if (!snapshot.Items.TryGetValue(key.ItemId, out var item))
                continue;
            if (!snapshot.Warehouses.TryGetValue(key.WarehouseId, out var warehouse))
                continue;

            var values = BuildSeries(snapshot.DailySales, key.ItemId, key.WarehouseId, start, historyDays);

            var avg7 = AverageLast(values, 7);
            var avg14 = AverageLast(values, 14);
            var trendNext = ForecastNextByTrend(values, 30);
            var forecastDaily = Math.Max(0, Math.Round(avg7 * 0.60m + trendNext * 0.40m, 3));
            if (forecastDaily <= 0 && avg14 > 0)
                forecastDaily = avg14;

            var forecastHorizonQty = Math.Round(forecastDaily * horizonDays, 2);
            var stdDev = StdDevLast(values, 30);
            var safetyStock = Math.Max(item.ReorderLevel, Math.Round((decimal)(1.65 * Math.Sqrt(leadTimeDays)) * stdDev, 2));
            var reorderPoint = Math.Round(forecastDaily * leadTimeDays + safetyStock, 2);

            var onHand = snapshot.OnHand.GetValueOrDefault((key.ItemId, key.WarehouseId));
            var suggestedQty = Math.Max(0, Math.Ceiling(forecastHorizonQty + safetyStock - onHand));
            var daysOfCover = forecastDaily > 0 ? Math.Round(onHand / forecastDaily, 1) : 999m;

            var confidence = ComputeConfidence(values, stdDev);
            var growthPct = ComputeGrowthPercent(values);
            var lastDemandDay = GetLastDemandDay(values, start);
            var shouldReorder = suggestedQty > 0 || onHand <= reorderPoint;
            var risk = daysOfCover < leadTimeDays || onHand <= reorderPoint
                ? "High"
                : daysOfCover < horizonDays
                    ? "Medium"
                    : "Low";

            rows.Add(new ForecastRow
            {
                ItemId = key.ItemId,
                ItemSku = item.Sku,
                ItemName = item.Name,
                WarehouseId = key.WarehouseId,
                WarehouseName = warehouse.Name,
                OnHand = onHand,
                ForecastDailyQty = forecastDaily,
                ForecastHorizonQty = forecastHorizonQty,
                LeadTimeDays = leadTimeDays,
                SafetyStock = safetyStock,
                ReorderPoint = reorderPoint,
                SuggestedReorderQty = suggestedQty,
                DaysOfCover = daysOfCover,
                Confidence = confidence,
                RiskLevel = risk,
                ShouldReorder = shouldReorder,
                GrowthPercent = growthPct,
                LastDemandDate = lastDemandDay,
                UnitPrice = item.UnitPrice
            });
        }

        return rows
            .OrderByDescending(r => r.ShouldReorder)
            .ThenByDescending(r => r.SuggestedReorderQty)
            .ThenBy(r => r.DaysOfCover)
            .ThenBy(r => r.ItemName)
            .ToList();
    }

    public async Task<List<SalesAnomalyRow>> DetectAnomaliesAsync(DateTime from, DateTime to, Guid? warehouseId = null)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start) (start, end) = (end, start);

        var snapshot = await LoadSnapshotAsync(start, end, warehouseId);
        if (snapshot.Keys.Count == 0)
            return new List<SalesAnomalyRow>();

        var historyDays = (end - start).Days + 1;
        var output = new List<SalesAnomalyRow>();

        foreach (var key in snapshot.Keys)
        {
            if (!snapshot.Items.TryGetValue(key.ItemId, out var item))
                continue;
            if (!snapshot.Warehouses.TryGetValue(key.WarehouseId, out var warehouse))
                continue;

            var values = BuildSeries(snapshot.DailySales, key.ItemId, key.WarehouseId, start, historyDays);
            if (values.Count < 20)
                continue;

            for (var i = 14; i < values.Count; i++)
            {
                var baseline = values.Skip(i - 14).Take(14).ToList();
                var expected = baseline.Count == 0 ? 0 : baseline.Average();
                var stdDev = StdDev(baseline);
                var actual = values[i];
                var deviation = actual - expected;

                decimal zScore;
                if (stdDev < 0.25m)
                {
                    if (expected <= 0)
                    {
                        if (actual < 5) continue;
                        zScore = 5;
                    }
                    else
                    {
                        var pct = Math.Abs(deviation) / expected;
                        if (pct < 1.5m) continue;
                        zScore = deviation / Math.Max(0.01m, expected);
                    }
                }
                else
                {
                    zScore = deviation / stdDev;
                    if (Math.Abs(zScore) < 2.5m) continue;
                }

                var severity = ClassifySeverity(zScore, deviation, expected);
                var day = start.AddDays(i);
                var deviationPct = expected > 0
                    ? Math.Round((deviation / expected) * 100m, 1)
                    : actual > 0 ? 999m : 0m;

                output.Add(new SalesAnomalyRow
                {
                    Date = day,
                    ItemId = key.ItemId,
                    ItemSku = item.Sku,
                    ItemName = item.Name,
                    WarehouseId = key.WarehouseId,
                    WarehouseName = warehouse.Name,
                    ExpectedQty = Math.Round(expected, 2),
                    ActualQty = Math.Round(actual, 2),
                    DeviationQty = Math.Round(deviation, 2),
                    DeviationPercent = deviationPct,
                    ZScore = Math.Round(zScore, 2),
                    Direction = deviation >= 0 ? "Spike" : "Drop",
                    Severity = severity
                });
            }
        }

        return output
            .OrderByDescending(a => SeverityRank(a.Severity))
            .ThenByDescending(a => Math.Abs(a.ZScore))
            .ThenByDescending(a => a.Date)
            .ToList();
    }

    private async Task<SalesSnapshot> LoadSnapshotAsync(DateTime from, DateTime to, Guid? warehouseId)
    {
        var posQuery = _db.PosBillLines
            .AsNoTracking()
            .Where(l => l.PosBill != null
                        && l.PosBill.Status == 2
                        && l.PosBill.BillDate >= from
                        && l.PosBill.BillDate <= to);
        if (warehouseId.HasValue)
            posQuery = posQuery.Where(l => l.PosBill!.WarehouseId == warehouseId.Value);

        var invQuery = _db.InvoiceLines
            .AsNoTracking()
            .Where(l => l.Invoice != null
                        && l.Invoice.Status == 2
                        && l.Invoice.InvoiceDate >= from
                        && l.Invoice.InvoiceDate <= to);
        if (warehouseId.HasValue)
            invQuery = invQuery.Where(l => l.Invoice!.WarehouseId == warehouseId.Value);

        var posLines = await posQuery
            .Select(l => new SalesLine
            {
                ItemId = l.ItemId,
                WarehouseId = l.PosBill!.WarehouseId,
                Date = l.PosBill.BillDate,
                Qty = l.Qty
            })
            .ToListAsync();

        var invLines = await invQuery
            .Select(l => new SalesLine
            {
                ItemId = l.ItemId,
                WarehouseId = l.Invoice!.WarehouseId,
                Date = l.Invoice.InvoiceDate,
                Qty = l.Qty
            })
            .ToListAsync();

        var stockQuery = _db.Stocks.AsNoTracking()
            .Include(s => s.Item)
            .Include(s => s.Warehouse)
            .AsQueryable();
        if (warehouseId.HasValue)
            stockQuery = stockQuery.Where(s => s.WarehouseId == warehouseId.Value);

        var stocks = await stockQuery.ToListAsync();

        var dailySales = new Dictionary<(Guid ItemId, Guid WarehouseId, DateOnly Day), decimal>();
        var keys = new HashSet<(Guid ItemId, Guid WarehouseId)>();
        var onHand = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in posLines)
            AddDailyDemand(dailySales, keys, line);
        foreach (var line in invLines)
            AddDailyDemand(dailySales, keys, line);

        foreach (var stock in stocks)
        {
            var key = (stock.ItemId, stock.WarehouseId);
            keys.Add(key);
            if (!onHand.ContainsKey(key))
                onHand[key] = 0;
            onHand[key] += stock.Quantity;
        }

        var itemIds = keys.Select(k => k.ItemId).Distinct().ToList();
        var warehouseIds = keys.Select(k => k.WarehouseId).Distinct().ToList();

        var items = await _db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.ItemId))
            .Select(i => new ItemMeta
            {
                ItemId = i.ItemId,
                Sku = i.SKU,
                Name = i.Name,
                ReorderLevel = i.ReorderLevel,
                UnitPrice = i.UnitPrice
            })
            .ToDictionaryAsync(i => i.ItemId);

        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.WarehouseId))
            .Select(w => new WarehouseMeta
            {
                WarehouseId = w.WarehouseId,
                Name = w.Name
            })
            .ToDictionaryAsync(w => w.WarehouseId);

        return new SalesSnapshot
        {
            DailySales = dailySales,
            Keys = keys.ToList(),
            OnHand = onHand,
            Items = items,
            Warehouses = warehouses
        };
    }

    private static void AddDailyDemand(
        Dictionary<(Guid ItemId, Guid WarehouseId, DateOnly Day), decimal> dailySales,
        HashSet<(Guid ItemId, Guid WarehouseId)> keys,
        SalesLine line)
    {
        var day = DateOnly.FromDateTime(line.Date.Date);
        var dayKey = (line.ItemId, line.WarehouseId, day);
        if (!dailySales.ContainsKey(dayKey))
            dailySales[dayKey] = 0;
        dailySales[dayKey] += line.Qty;

        keys.Add((line.ItemId, line.WarehouseId));
    }

    private static List<decimal> BuildSeries(
        Dictionary<(Guid ItemId, Guid WarehouseId, DateOnly Day), decimal> dailySales,
        Guid itemId,
        Guid warehouseId,
        DateTime start,
        int historyDays)
    {
        var values = new List<decimal>(historyDays);
        for (var i = 0; i < historyDays; i++)
        {
            var day = DateOnly.FromDateTime(start.AddDays(i));
            values.Add(dailySales.GetValueOrDefault((itemId, warehouseId, day)));
        }
        return values;
    }

    private static decimal AverageLast(List<decimal> values, int window)
    {
        if (values.Count == 0) return 0;
        var take = Math.Min(window, values.Count);
        var subset = values.Skip(values.Count - take).Take(take).ToList();
        return subset.Count == 0 ? 0 : Math.Round(subset.Average(), 3);
    }

    private static decimal ForecastNextByTrend(List<decimal> values, int window)
    {
        if (values.Count == 0) return 0;
        var take = Math.Min(window, values.Count);
        var subset = values.Skip(values.Count - take).Take(take).ToList();
        if (subset.Count <= 1) return subset.FirstOrDefault();

        decimal xMean = (subset.Count - 1) / 2m;
        var yMean = subset.Average();
        decimal numerator = 0;
        decimal denominator = 0;

        for (var i = 0; i < subset.Count; i++)
        {
            var dx = i - xMean;
            var dy = subset[i] - yMean;
            numerator += dx * dy;
            denominator += dx * dx;
        }

        var slope = denominator == 0 ? 0 : numerator / denominator;
        var intercept = yMean - slope * xMean;
        var next = intercept + slope * subset.Count;
        return Math.Max(0, Math.Round(next, 3));
    }

    private static decimal StdDevLast(List<decimal> values, int window)
    {
        if (values.Count == 0) return 0;
        var take = Math.Min(window, values.Count);
        return StdDev(values.Skip(values.Count - take).Take(take).ToList());
    }

    private static decimal StdDev(List<decimal> values)
    {
        if (values.Count <= 1) return 0;
        var mean = values.Average();
        var variance = values.Select(v => (v - mean) * (v - mean)).Average();
        return (decimal)Math.Sqrt((double)variance);
    }

    private static decimal ComputeGrowthPercent(List<decimal> values)
    {
        if (values.Count < 14) return 0;
        var recent = values.Skip(values.Count - 7).Take(7).Average();
        var previous = values.Skip(values.Count - 14).Take(7).Average();
        if (previous <= 0) return recent > 0 ? 100 : 0;
        return Math.Round(((recent - previous) / previous) * 100m, 1);
    }

    private static decimal ComputeConfidence(List<decimal> values, decimal stdDev)
    {
        if (values.Count == 0) return 0;

        var historyScore = Math.Min(1m, values.Count / 60m);
        var avg = values.Average();
        var volatility = avg <= 0 ? 1m : Math.Min(1m, stdDev / (avg + 0.01m));
        var activeDays = values.Count(v => v > 0);
        var activityScore = Math.Min(1m, activeDays / 20m);

        var score = (historyScore * 0.45m) + ((1m - volatility) * 0.35m) + (activityScore * 0.20m);
        return Math.Round(Math.Clamp(score, 0m, 1m) * 100m, 1);
    }

    private static DateTime? GetLastDemandDay(List<decimal> values, DateTime start)
    {
        for (var i = values.Count - 1; i >= 0; i--)
        {
            if (values[i] > 0)
                return start.AddDays(i);
        }
        return null;
    }

    private static string ClassifySeverity(decimal zScore, decimal deviation, decimal expected)
    {
        var absZ = Math.Abs(zScore);
        if (absZ >= 4.5m) return "Critical";
        if (absZ >= 3.5m) return "High";
        if (absZ >= 2.5m) return "Medium";

        if (expected <= 0 && Math.Abs(deviation) >= 10) return "High";
        return "Low";
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 4,
        "High" => 3,
        "Medium" => 2,
        _ => 1
    };

    private sealed class SalesLine
    {
        public Guid ItemId { get; set; }
        public Guid WarehouseId { get; set; }
        public DateTime Date { get; set; }
        public decimal Qty { get; set; }
    }

    private sealed class SalesSnapshot
    {
        public Dictionary<(Guid ItemId, Guid WarehouseId, DateOnly Day), decimal> DailySales { get; set; } = new();
        public List<(Guid ItemId, Guid WarehouseId)> Keys { get; set; } = new();
        public Dictionary<(Guid ItemId, Guid WarehouseId), decimal> OnHand { get; set; } = new();
        public Dictionary<Guid, ItemMeta> Items { get; set; } = new();
        public Dictionary<Guid, WarehouseMeta> Warehouses { get; set; } = new();
    }

    private sealed class ItemMeta
    {
        public Guid ItemId { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public int ReorderLevel { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private sealed class WarehouseMeta
    {
        public Guid WarehouseId { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class ForecastRow
    {
        public Guid ItemId { get; set; }
        public string ItemSku { get; set; } = "";
        public string ItemName { get; set; } = "";
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = "";
        public decimal OnHand { get; set; }
        public decimal ForecastDailyQty { get; set; }
        public decimal ForecastHorizonQty { get; set; }
        public int LeadTimeDays { get; set; }
        public decimal SafetyStock { get; set; }
        public decimal ReorderPoint { get; set; }
        public decimal SuggestedReorderQty { get; set; }
        public decimal DaysOfCover { get; set; }
        public decimal Confidence { get; set; }
        public string RiskLevel { get; set; } = "Low";
        public bool ShouldReorder { get; set; }
        public decimal GrowthPercent { get; set; }
        public DateTime? LastDemandDate { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class SalesAnomalyRow
    {
        public DateTime Date { get; set; }
        public Guid ItemId { get; set; }
        public string ItemSku { get; set; } = "";
        public string ItemName { get; set; } = "";
        public Guid WarehouseId { get; set; }
        public string WarehouseName { get; set; } = "";
        public decimal ExpectedQty { get; set; }
        public decimal ActualQty { get; set; }
        public decimal DeviationQty { get; set; }
        public decimal DeviationPercent { get; set; }
        public decimal ZScore { get; set; }
        public string Direction { get; set; } = "";
        public string Severity { get; set; } = "Low";
    }
}

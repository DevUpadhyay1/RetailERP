using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class ForecastController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ForecastService _forecast;

    public ForecastController(ApplicationDbContext db, ForecastService forecast)
    {
        _db = db;
        _forecast = forecast;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from,
        DateTime? to,
        int horizonDays = 14,
        int leadTimeDays = 7,
        Guid? warehouseId = null)
    {
        var (start, end) = ResolveRange(from, to, defaultDays: 90);
        horizonDays = Math.Clamp(horizonDays, 1, 90);
        leadTimeDays = Math.Clamp(leadTimeDays, 1, 30);

        var forecastRows = await _forecast.BuildForecastAsync(start, end, horizonDays, leadTimeDays, warehouseId);
        var anomalies = await _forecast.DetectAnomaliesAsync(start, end, warehouseId);

        var topReorders = forecastRows.Where(r => r.ShouldReorder).Take(10).ToList();
        if (topReorders.Count == 0)
            topReorders = forecastRows.Take(10).ToList();

        var vm = new ForecastDashboardVm
        {
            From = start,
            To = end,
            HorizonDays = horizonDays,
            LeadTimeDays = leadTimeDays,
            WarehouseId = warehouseId,
            Warehouses = await LoadWarehousesAsync(),
            TotalItems = forecastRows.Count,
            ReorderItems = forecastRows.Count(r => r.ShouldReorder),
            HighRiskItems = forecastRows.Count(r => string.Equals(r.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)),
            TotalForecastQty = forecastRows.Sum(r => r.ForecastHorizonQty),
            TotalSuggestedQty = forecastRows.Sum(r => r.SuggestedReorderQty),
            TotalSuggestedValue = forecastRows.Sum(r => r.SuggestedReorderQty * r.UnitPrice),
            AnomalyCount = anomalies.Count,
            CriticalAnomalyCount = anomalies.Count(a =>
                string.Equals(a.Severity, "Critical", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Severity, "High", StringComparison.OrdinalIgnoreCase)),
            TopReorders = topReorders,
            RecentAnomalies = anomalies.Take(10).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Reorder(
        DateTime? from,
        DateTime? to,
        int horizonDays = 14,
        int leadTimeDays = 7,
        Guid? warehouseId = null,
        string? q = null,
        string? risk = null,
        bool onlyReorder = true,
        string sort = "suggested",
        string dir = "desc",
        int page = 1,
        int pageSize = 25)
    {
        var (start, end) = ResolveRange(from, to, defaultDays: 90);
        horizonDays = Math.Clamp(horizonDays, 1, 90);
        leadTimeDays = Math.Clamp(leadTimeDays, 1, 30);
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 25;

        var rows = await _forecast.BuildForecastAsync(start, end, horizonDays, leadTimeDays, warehouseId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            rows = rows.Where(r =>
                    r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.ItemSku.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(risk))
            rows = rows.Where(r => string.Equals(r.RiskLevel, risk, StringComparison.OrdinalIgnoreCase)).ToList();

        if (onlyReorder)
            rows = rows.Where(r => r.ShouldReorder).ToList();

        rows = ApplyReorderSort(rows, sort, dir).ToList();

        var total = rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var paged = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var vm = new ForecastReorderVm
        {
            From = start,
            To = end,
            HorizonDays = horizonDays,
            LeadTimeDays = leadTimeDays,
            WarehouseId = warehouseId,
            Warehouses = await LoadWarehousesAsync(),
            Q = q?.Trim(),
            Risk = risk,
            OnlyReorder = onlyReorder,
            Sort = sort,
            Dir = dir,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            FromRow = total == 0 ? 0 : ((page - 1) * pageSize + 1),
            ToRow = Math.Min(page * pageSize, total),
            ItemsNeedingReorder = rows.Count(r => r.ShouldReorder),
            TotalSuggestedQty = rows.Sum(r => r.SuggestedReorderQty),
            TotalSuggestedValue = rows.Sum(r => r.SuggestedReorderQty * r.UnitPrice),
            HighRiskItems = rows.Count(r => string.Equals(r.RiskLevel, "High", StringComparison.OrdinalIgnoreCase)),
            AvgConfidence = rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.Confidence), 1),
            Rows = paged
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Anomalies(
        DateTime? from,
        DateTime? to,
        Guid? warehouseId = null,
        string? q = null,
        string? severity = null,
        string? direction = null,
        string sort = "severity",
        string dir = "desc",
        int page = 1,
        int pageSize = 25)
    {
        var (start, end) = ResolveRange(from, to, defaultDays: 90);
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 25;

        var rows = await _forecast.DetectAnomaliesAsync(start, end, warehouseId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            rows = rows.Where(r =>
                    r.ItemName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.ItemSku.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.WarehouseName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(severity))
            rows = rows.Where(r => string.Equals(r.Severity, severity, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(direction))
            rows = rows.Where(r => string.Equals(r.Direction, direction, StringComparison.OrdinalIgnoreCase)).ToList();

        rows = ApplyAnomalySort(rows, sort, dir).ToList();

        var total = rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page > totalPages) page = totalPages;

        var paged = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var vm = new ForecastAnomalyVm
        {
            From = start,
            To = end,
            WarehouseId = warehouseId,
            Warehouses = await LoadWarehousesAsync(),
            Q = q?.Trim(),
            Severity = severity,
            Direction = direction,
            Sort = sort,
            Dir = dir,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages,
            FromRow = total == 0 ? 0 : ((page - 1) * pageSize + 1),
            ToRow = Math.Min(page * pageSize, total),
            CriticalOrHigh = rows.Count(r => SeverityRank(r.Severity) >= 3),
            Spikes = rows.Count(r => string.Equals(r.Direction, "Spike", StringComparison.OrdinalIgnoreCase)),
            Drops = rows.Count(r => string.Equals(r.Direction, "Drop", StringComparison.OrdinalIgnoreCase)),
            Rows = paged
        };

        return View(vm);
    }

    private async Task<List<WarehouseOption>> LoadWarehousesAsync()
    {
        return await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseOption
            {
                WarehouseId = w.WarehouseId,
                Name = w.Name
            })
            .ToListAsync();
    }

    private static (DateTime From, DateTime To) ResolveRange(DateTime? from, DateTime? to, int defaultDays)
    {
        var end = (to ?? DateTime.Today).Date;
        var start = (from ?? end.AddDays(-defaultDays)).Date;
        if (end < start) (start, end) = (end, start);
        return (start, end);
    }

    private static IEnumerable<ForecastService.ForecastRow> ApplyReorderSort(
        IEnumerable<ForecastService.ForecastRow> rows,
        string sort,
        string dir)
    {
        var ascending = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        sort = (sort ?? "suggested").ToLowerInvariant();

        return sort switch
        {
            "sku" => ascending
                ? rows.OrderBy(r => r.ItemSku).ThenBy(r => r.WarehouseName)
                : rows.OrderByDescending(r => r.ItemSku).ThenByDescending(r => r.WarehouseName),
            "item" => ascending
                ? rows.OrderBy(r => r.ItemName).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.ItemName).ThenByDescending(r => r.ItemSku),
            "warehouse" => ascending
                ? rows.OrderBy(r => r.WarehouseName).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.WarehouseName).ThenByDescending(r => r.ItemSku),
            "onhand" => ascending
                ? rows.OrderBy(r => r.OnHand).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.OnHand).ThenByDescending(r => r.ItemSku),
            "daily" => ascending
                ? rows.OrderBy(r => r.ForecastDailyQty).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.ForecastDailyQty).ThenByDescending(r => r.ItemSku),
            "reorderpoint" => ascending
                ? rows.OrderBy(r => r.ReorderPoint).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.ReorderPoint).ThenByDescending(r => r.ItemSku),
            "cover" => ascending
                ? rows.OrderBy(r => r.DaysOfCover).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.DaysOfCover).ThenByDescending(r => r.ItemSku),
            "confidence" => ascending
                ? rows.OrderBy(r => r.Confidence).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.Confidence).ThenByDescending(r => r.ItemSku),
            "growth" => ascending
                ? rows.OrderBy(r => r.GrowthPercent).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.GrowthPercent).ThenByDescending(r => r.ItemSku),
            "value" => ascending
                ? rows.OrderBy(r => r.SuggestedReorderQty * r.UnitPrice).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.SuggestedReorderQty * r.UnitPrice).ThenByDescending(r => r.ItemSku),
            _ => ascending
                ? rows.OrderBy(r => r.SuggestedReorderQty).ThenBy(r => r.ItemSku)
                : rows.OrderByDescending(r => r.SuggestedReorderQty).ThenByDescending(r => r.ItemSku)
        };
    }

    private static IEnumerable<ForecastService.SalesAnomalyRow> ApplyAnomalySort(
        IEnumerable<ForecastService.SalesAnomalyRow> rows,
        string sort,
        string dir)
    {
        var ascending = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        sort = (sort ?? "severity").ToLowerInvariant();

        return sort switch
        {
            "date" => ascending
                ? rows.OrderBy(r => r.Date)
                : rows.OrderByDescending(r => r.Date),
            "sku" => ascending
                ? rows.OrderBy(r => r.ItemSku).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => r.ItemSku).ThenByDescending(r => r.Date),
            "item" => ascending
                ? rows.OrderBy(r => r.ItemName).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => r.ItemName).ThenByDescending(r => r.Date),
            "warehouse" => ascending
                ? rows.OrderBy(r => r.WarehouseName).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => r.WarehouseName).ThenByDescending(r => r.Date),
            "zscore" => ascending
                ? rows.OrderBy(r => Math.Abs(r.ZScore)).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => Math.Abs(r.ZScore)).ThenByDescending(r => r.Date),
            "deviation" => ascending
                ? rows.OrderBy(r => Math.Abs(r.DeviationPercent)).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => Math.Abs(r.DeviationPercent)).ThenByDescending(r => r.Date),
            _ => ascending
                ? rows.OrderBy(r => SeverityRank(r.Severity)).ThenBy(r => r.Date)
                : rows.OrderByDescending(r => SeverityRank(r.Severity)).ThenByDescending(r => r.Date)
        };
    }

    private static int SeverityRank(string? severity) => severity?.ToLowerInvariant() switch
    {
        "critical" => 4,
        "high" => 3,
        "medium" => 2,
        _ => 1
    };

    public sealed class WarehouseOption
    {
        public Guid WarehouseId { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class ForecastDashboardVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int HorizonDays { get; set; }
        public int LeadTimeDays { get; set; }
        public Guid? WarehouseId { get; set; }
        public List<WarehouseOption> Warehouses { get; set; } = new();
        public int TotalItems { get; set; }
        public int ReorderItems { get; set; }
        public int HighRiskItems { get; set; }
        public decimal TotalForecastQty { get; set; }
        public decimal TotalSuggestedQty { get; set; }
        public decimal TotalSuggestedValue { get; set; }
        public int AnomalyCount { get; set; }
        public int CriticalAnomalyCount { get; set; }
        public List<ForecastService.ForecastRow> TopReorders { get; set; } = new();
        public List<ForecastService.SalesAnomalyRow> RecentAnomalies { get; set; } = new();
    }

    public sealed class ForecastReorderVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int HorizonDays { get; set; }
        public int LeadTimeDays { get; set; }
        public Guid? WarehouseId { get; set; }
        public List<WarehouseOption> Warehouses { get; set; } = new();
        public string? Q { get; set; }
        public string? Risk { get; set; }
        public bool OnlyReorder { get; set; }
        public string Sort { get; set; } = "suggested";
        public string Dir { get; set; } = "desc";
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public int FromRow { get; set; }
        public int ToRow { get; set; }
        public int ItemsNeedingReorder { get; set; }
        public decimal TotalSuggestedQty { get; set; }
        public decimal TotalSuggestedValue { get; set; }
        public int HighRiskItems { get; set; }
        public decimal AvgConfidence { get; set; }
        public List<ForecastService.ForecastRow> Rows { get; set; } = new();
    }

    public sealed class ForecastAnomalyVm
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public Guid? WarehouseId { get; set; }
        public List<WarehouseOption> Warehouses { get; set; } = new();
        public string? Q { get; set; }
        public string? Severity { get; set; }
        public string? Direction { get; set; }
        public string Sort { get; set; } = "severity";
        public string Dir { get; set; } = "desc";
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
        public int FromRow { get; set; }
        public int ToRow { get; set; }
        public int CriticalOrHigh { get; set; }
        public int Spikes { get; set; }
        public int Drops { get; set; }
        public List<ForecastService.SalesAnomalyRow> Rows { get; set; } = new();
    }
}

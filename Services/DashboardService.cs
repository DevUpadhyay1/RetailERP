using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>Sprint 3 – Provides widget data and layout persistence.</summary>
public sealed class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly CacheService _cache;
    public DashboardService(ApplicationDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // ═══════════════════════════════════════════════════
    //  Layout CRUD
    // ═══════════════════════════════════════════════════

    public async Task<List<WidgetPlacement>> GetLayoutAsync(Guid userId, BusinessType biz, string role)
    {
        return await _cache.GetOrSetAsync($"layout:{userId}", async () =>
        {
            var layout = await _db.UserDashboardLayouts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (layout is not null)
                return JsonSerializer.Deserialize<List<WidgetPlacement>>(layout.LayoutJson) ?? new();

            return DashboardWidgetCatalog.GetDefaultLayout(biz, role);
        }, TimeSpan.FromHours(1));
    }

    public async Task SaveLayoutAsync(Guid userId, List<WidgetPlacement> placements)
    {
        var layout = await _db.UserDashboardLayouts
            .FirstOrDefaultAsync(x => x.UserId == userId);

        var json = JsonSerializer.Serialize(placements);

        if (layout is null)
        {
            _db.UserDashboardLayouts.Add(new UserDashboardLayout
            {
                UserId = userId,
                LayoutJson = json,
                LastModifiedUtc = DateTime.UtcNow
            });
        }
        else
        {
            layout.LayoutJson = json;
            layout.LastModifiedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        await _cache.RemoveAsync($"layout:{userId}");
    }

    public async Task ResetLayoutAsync(Guid userId)
    {
        var layout = await _db.UserDashboardLayouts
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (layout is not null)
        {
            _db.UserDashboardLayouts.Remove(layout);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync($"layout:{userId}");
        }
    }

    // ═══════════════════════════════════════════════════
    //  Widget Data — cached via Redis
    // ═══════════════════════════════════════════════════

    public async Task<object> GetWidgetDataAsync(string widgetId, int monthOffset = 0)
    {
        monthOffset = Math.Clamp(monthOffset, -24, 0);

        return await _cache.GetOrSetAsync($"widget:{widgetId}:m:{monthOffset}", async () =>
        {
            var targetMonth = DateTime.Today.AddMonths(monthOffset);
            var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var fromUtc7 = DateTime.UtcNow.Date.AddDays(-7);
            var daysInMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);
            var monthLabel = monthStart.ToString("MMM yyyy");

            return widgetId switch
            {
            "total-sales" => new
            {
                value = (await _db.Invoices.AsNoTracking()
                        .Where(x => x.Status == 2 && x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                        .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m)
                      + (await _db.PosBills.AsNoTracking()
                        .Where(x => x.Status == 2 && x.BillDate >= monthStart && x.BillDate < monthEnd)
                        .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m),
                                label = $"Total Sales ({monthLabel})"
            },

            "pos-sales" => new
            {
                value = await _db.PosBills.AsNoTracking()
                    .Where(x => x.Status == 2 && x.BillDate >= monthStart && x.BillDate < monthEnd)
                    .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m,
                label = $"POS Sales ({monthLabel})"
            },

            "purchases-month" => new
            {
                value = await _db.Purchases.AsNoTracking()
                    .Where(x => x.Status == 2 && x.PurchaseDate >= monthStart && x.PurchaseDate < monthEnd)
                    .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m,
                label = $"Purchases ({monthLabel})"
            },

            "low-stock-count" => new
            {
                value = await LowStockReporting.CountAsync(_db),
                label = "Low Stock Items"
            },

            "open-pos-bills" => new
            {
                value = await _db.PosBills.AsNoTracking().CountAsync(x => x.Status == 1),
                label = "Open POS Bills"
            },

            "completed-bills-7d" => new
            {
                value = await _db.PosBills.AsNoTracking()
                    .CountAsync(x => x.Status == 2 && x.CompletedAtUtc != null && x.CompletedAtUtc >= fromUtc7),
                label = "Completed Bills (7 Days)"
            },

            "items-count" => new
            {
                value = await _db.Items.AsNoTracking().CountAsync(),
                label = "Total Items"
            },

            "customers-count" => new
            {
                value = await _db.Customers.AsNoTracking().CountAsync(),
                label = "Total Customers"
            },

            "employees-count" => new
            {
                value = await _db.Employees.AsNoTracking().CountAsync(),
                label = "Total Employees"
            },

            "warehouses-count" => new
            {
                value = await _db.Warehouses.AsNoTracking().CountAsync(),
                label = "Warehouses"
            },

            "draft-invoices" => new
            {
                value = await _db.Invoices.AsNoTracking().CountAsync(x => x.Status == 1),
                label = "Draft Invoices"
            },

            "sales-7d" => new
            {
                value = (await _db.Invoices.AsNoTracking()
                        .Where(x => x.Status == 2 && x.PostedAt != null && x.PostedAt >= fromUtc7)
                        .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m)
                      + (await _db.PosBills.AsNoTracking()
                        .Where(x => x.Status == 2 && x.CompletedAtUtc != null && x.CompletedAtUtc >= fromUtc7)
                        .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m),
                label = "Sales Last 7 Days"
            },

            "loyalty-members" => new
            {
                value = await _db.LoyaltyCards.AsNoTracking().CountAsync(x => x.IsActive),
                label = "Loyalty Members"
            },

            "active-coupons" => new
            {
                value = await _db.Coupons.AsNoTracking()
                    .CountAsync(x => x.IsActive && x.ValidTo > DateTime.Today),
                label = "Active Coupons"
            },

            "returns-pending" => new
            {
                value = await _db.PosReturns.AsNoTracking().CountAsync(x => x.Status == 1),
                label = "Pending Returns"
            },

            // ───── Charts ─────

            "sales-purchases-chart" => await GetSalesPurchasesChartAsync(monthStart, monthEnd, daysInMonth),

            "monthly-sales-trend" => await GetMonthlySalesTrendAsync(monthStart),

            "sales-channel-mix" => await GetSalesChannelMixAsync(monthStart, monthEnd),

            "weekday-sales-trend" => await GetWeekdaySalesTrendAsync(monthStart, monthEnd),

            "pos-hourly-chart" => await GetPosHourlyChartAsync(),

            "category-pie" => await GetCategoryPieAsync(monthStart, monthEnd),

            "payment-method-pie" => await GetPaymentMethodPieAsync(monthStart, monthEnd),

            "top-items-bar" => await GetTopItemsBarAsync(monthStart, monthEnd),

            // ───── Tables ─────

            "low-stock-list" => new
            {
                rows = await LowStockReporting.Query(_db)
                    .OrderBy(x => x.OnHand).Take(10)
                    .Select(x => new
                    {
                        sku = x.SKU,
                        name = x.Name,
                        warehouse = "All",
                        qty = x.OnHand,
                        reorder = x.ReorderLevel
                    }).ToListAsync()
            },

            "recent-invoices" => new
            {
                rows = await _db.Invoices.AsNoTracking()
                    .OrderByDescending(x => x.InvoiceDate).ThenByDescending(x => x.InvoiceNo)
                    .Take(5)
                    .Select(x => new
                    {
                        id = x.InvoiceId,
                        no = x.InvoiceNo,
                        date = x.InvoiceDate.ToString("dd-MMM-yyyy"),
                        customer = x.Customer != null ? x.Customer.Name : "-",
                        warehouse = x.Warehouse != null ? x.Warehouse.Name : "-",
                        amount = x.TotalAmount,
                        status = (int)x.Status
                    }).ToListAsync()
            },

            "recent-pos-bills" => new
            {
                rows = await _db.PosBills.AsNoTracking()
                    .OrderByDescending(x => x.BillDate).ThenByDescending(x => x.BillNo)
                    .Take(5)
                    .Select(x => new
                    {
                        id = x.PosBillId,
                        no = x.BillNo,
                        date = x.BillDate.ToString("dd-MMM-yyyy"),
                        customer = x.Customer != null ? x.Customer.Name : "Walk-in",
                        store = x.Store != null ? x.Store.Name : "-",
                        amount = x.GrandTotal,
                        status = (int)x.Status
                    }).ToListAsync()
            },

            "expiring-items" => new
            {
                rows = await _db.Stocks.AsNoTracking()
                    .Where(s => s.ExpiryDate != null && s.ExpiryDate <= DateTime.Today.AddDays(90) && s.Quantity > 0)
                    .OrderBy(s => s.ExpiryDate)
                    .Take(10)
                    .Select(s => new
                    {
                        itemName = s.Item != null ? s.Item.Name : "-",
                        sku = s.Item != null ? s.Item.SKU : "-",
                        warehouse = s.Warehouse != null ? s.Warehouse.Name : "-",
                        batch = s.BatchNumber ?? "-",
                        expiryDate = s.ExpiryDate!.Value.ToString("dd-MMM-yyyy"),
                        daysLeft = (s.ExpiryDate!.Value - DateTime.Today).Days,
                        qty = s.Quantity
                    }).ToListAsync()
            },

            "eod-summary" => new
            {
                rows = await _db.EodReports.AsNoTracking()
                    .Where(e => e.ReportDate == DateTime.Today)
                    .Select(e => new
                    {
                        store = e.Store != null ? e.Store.Name : "-",
                        date = e.ReportDate.ToString("dd-MMM-yyyy"),
                        totalSales = e.TotalSales,
                        totalReturns = e.TotalReturns,
                        netSales = e.NetSales,
                        status = (int)e.Status
                    }).ToListAsync()
            },

            _ => new { error = "Unknown widget" }
        };
        }, TimeSpan.FromMinutes(5));
    }

    // ── Chart helpers ──

    private async Task<object> GetSalesPurchasesChartAsync(DateTime monthStart, DateTime monthEnd, int daysInMonth)
    {
        var labels = Enumerable.Range(1, daysInMonth)
            .Select(i => i.ToString("00")).ToList();

        var previousMonthStart = monthStart.AddMonths(-1);
        var previousMonthEnd = monthStart;
        var previousDaysInMonth = DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month);

        var salesByDay = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status == 2 && x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
            .GroupBy(x => x.InvoiceDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();
        var posMap = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= monthStart && x.BillDate < monthEnd)
            .GroupBy(x => x.BillDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync();
        var purchMap = await _db.Purchases.AsNoTracking()
            .Where(x => x.Status == 2 && x.PurchaseDate >= monthStart && x.PurchaseDate < monthEnd)
            .GroupBy(x => x.PurchaseDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var prevInvoiceByDay = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status == 2 && x.InvoiceDate >= previousMonthStart && x.InvoiceDate < previousMonthEnd)
            .GroupBy(x => x.InvoiceDate.Day)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();
        var prevPosByDay = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= previousMonthStart && x.BillDate < previousMonthEnd)
            .GroupBy(x => x.BillDate.Day)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync();

        var sMap = salesByDay.ToDictionary(x => x.Day.ToString("yyyy-MM-dd"), x => x.Total);
        var pMap = posMap.ToDictionary(x => x.Day.ToString("yyyy-MM-dd"), x => x.Total);
        var prMap = purchMap.ToDictionary(x => x.Day.ToString("yyyy-MM-dd"), x => x.Total);

        var prevMap = prevInvoiceByDay
            .Concat(prevPosByDay)
            .GroupBy(x => x.Day)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

        return new
        {
            labels,
            invoiceSales = labels.Select(d =>
            {
                var date = $"{monthStart:yyyy-MM}-{d}";
                return sMap.TryGetValue(date, out var v) ? v : 0m;
            }).ToList(),
            posSales = labels.Select(d =>
            {
                var date = $"{monthStart:yyyy-MM}-{d}";
                return pMap.TryGetValue(date, out var v) ? v : 0m;
            }).ToList(),
            purchases = labels.Select(d =>
            {
                var date = $"{monthStart:yyyy-MM}-{d}";
                return prMap.TryGetValue(date, out var v) ? v : 0m;
            }).ToList(),
            prevMonthSales = labels.Select(d =>
            {
                var day = int.Parse(d);
                if (day > previousDaysInMonth) return 0m;
                return prevMap.TryGetValue(day, out var v) ? v : 0m;
            }).ToList(),
            currentMonthLabel = monthStart.ToString("MMM yyyy"),
            previousMonthLabel = previousMonthStart.ToString("MMM yyyy")
        };
    }

    private async Task<object> GetPosHourlyChartAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var hourly = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= today && x.BillDate < tomorrow)
            .GroupBy(x => x.BillDate.Hour)
            .Select(g => new { Hour = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync();

        var map = hourly.ToDictionary(x => x.Hour, x => x.Total);
        var labels = Enumerable.Range(6, 18).Select(h => $"{h:00}:00").ToList();
        var data = Enumerable.Range(6, 18).Select(h => map.TryGetValue(h, out var v) ? v : 0m).ToList();

        return new { labels, data };
    }

    private async Task<object> GetCategoryPieAsync(DateTime monthStart, DateTime monthEnd)
    {
        var data = await _db.PosBillLines.AsNoTracking()
            .Where(l => l.PosBill!.Status == 2 && l.PosBill.BillDate >= monthStart && l.PosBill.BillDate < monthEnd)
            .GroupBy(l => l.Item!.Category!.Name)
            .Select(g => new { category = g.Key ?? "Uncategorized", total = g.Sum(l => l.LineTotal) })
            .OrderByDescending(x => x.total).Take(8)
            .ToListAsync();

        return new { labels = data.Select(x => x.category).ToList(), data = data.Select(x => x.total).ToList() };
    }

    private async Task<object> GetPaymentMethodPieAsync(DateTime monthStart, DateTime monthEnd)
    {
        var data = await _db.Payments.AsNoTracking()
            .Where(p => p.PaidAtUtc >= monthStart.ToUniversalTime() && p.PaidAtUtc < monthEnd.ToUniversalTime())
            .GroupBy(p => p.Method)
            .Select(g => new { method = g.Key, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        return new { labels = data.Select(x => x.method).ToList(), data = data.Select(x => x.total).ToList() };
    }

    private async Task<object> GetTopItemsBarAsync(DateTime monthStart, DateTime monthEnd)
    {
        var data = await _db.PosBillLines.AsNoTracking()
            .Where(l => l.PosBill!.Status == 2 && l.PosBill.BillDate >= monthStart && l.PosBill.BillDate < monthEnd)
            .GroupBy(l => l.Item!.Name)
            .Select(g => new { item = g.Key ?? "?", qty = g.Sum(l => l.Qty) })
            .OrderByDescending(x => x.qty).Take(10)
            .ToListAsync();

        return new { labels = data.Select(x => x.item).ToList(), data = data.Select(x => x.qty).ToList() };
    }

    private async Task<object> GetMonthlySalesTrendAsync(DateTime monthStart)
    {
        var trendStart = monthStart.AddMonths(-11);
        var trendEnd = monthStart.AddMonths(1);

        var labels = Enumerable.Range(0, 12)
            .Select(i => trendStart.AddMonths(i).ToString("MMM yy"))
            .ToList();
        var monthKeys = Enumerable.Range(0, 12)
            .Select(i => trendStart.AddMonths(i).ToString("yyyy-MM"))
            .ToList();

        var invoiceMonthly = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status == 2 && x.InvoiceDate >= trendStart && x.InvoiceDate < trendEnd)
            .GroupBy(x => new { x.InvoiceDate.Year, x.InvoiceDate.Month })
            .Select(g => new { Key = $"{g.Key.Year}-{g.Key.Month:00}", Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var posMonthly = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= trendStart && x.BillDate < trendEnd)
            .GroupBy(x => new { x.BillDate.Year, x.BillDate.Month })
            .Select(g => new { Key = $"{g.Key.Year}-{g.Key.Month:00}", Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync();

        var invMap = invoiceMonthly.ToDictionary(x => x.Key, x => x.Total);
        var posMap = posMonthly.ToDictionary(x => x.Key, x => x.Total);

        var invoiceSeries = monthKeys.Select(k => invMap.TryGetValue(k, out var v) ? v : 0m).ToList();
        var posSeries = monthKeys.Select(k => posMap.TryGetValue(k, out var v) ? v : 0m).ToList();
        var totalSeries = monthKeys
            .Select(k => (invMap.TryGetValue(k, out var i) ? i : 0m) + (posMap.TryGetValue(k, out var p) ? p : 0m))
            .ToList();

        return new
        {
            labels,
            invoiceSales = invoiceSeries,
            posSales = posSeries,
            totalSales = totalSeries,
            currentMonthLabel = monthStart.ToString("MMM yyyy")
        };
    }

    private async Task<object> GetSalesChannelMixAsync(DateTime monthStart, DateTime monthEnd)
    {
        var invoiceTotal = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status == 2 && x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
            .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

        var posTotal = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= monthStart && x.BillDate < monthEnd)
            .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m;

        var returnTotal = await _db.PosReturns.AsNoTracking()
            .Where(x => x.Status == 2 && x.ReturnDate >= monthStart && x.ReturnDate < monthEnd)
            .SumAsync(x => (decimal?)x.TotalRefund) ?? 0m;

        return new
        {
            labels = new[] { "Invoice", "POS", "Returns" },
            data = new[] { invoiceTotal, posTotal, returnTotal },
            total = invoiceTotal + posTotal,
            monthLabel = monthStart.ToString("MMM yyyy")
        };
    }

    private async Task<object> GetWeekdaySalesTrendAsync(DateTime monthStart, DateTime monthEnd)
    {
        var invoiceDaily = await _db.Invoices.AsNoTracking()
            .Where(x => x.Status == 2 && x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
            .GroupBy(x => x.InvoiceDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var posDaily = await _db.PosBills.AsNoTracking()
            .Where(x => x.Status == 2 && x.BillDate >= monthStart && x.BillDate < monthEnd)
            .GroupBy(x => x.BillDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToListAsync();

        var map = Enumerable.Range(0, 7).ToDictionary(i => i, _ => 0m);

        foreach (var d in invoiceDaily)
        {
            var idx = ((int)d.Day.DayOfWeek + 6) % 7;
            map[idx] += d.Total;
        }

        foreach (var d in posDaily)
        {
            var idx = ((int)d.Day.DayOfWeek + 6) % 7;
            map[idx] += d.Total;
        }

        return new
        {
            labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
            data = Enumerable.Range(0, 7).Select(i => map[i]).ToList(),
            monthLabel = monthStart.ToString("MMM yyyy")
        };
    }
}

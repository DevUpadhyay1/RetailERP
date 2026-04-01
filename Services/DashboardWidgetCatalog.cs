using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>Sprint 3 – Master catalog of all dashboard widgets.</summary>
public static class DashboardWidgetCatalog
{
    public static readonly IReadOnlyList<WidgetDefinition> All = new List<WidgetDefinition>
    {
        // ───── KPI Cards (1×1) ─────
        new("total-sales",       "Total Sales (Month)",      "bi-currency-rupee", WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("pos-sales",         "POS Sales (Month)",        "bi-shop-window",    WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),

        new("purchases-month",   "Purchases (Month)",        "bi-cart4",          WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("low-stock-count",   "Low Stock Items",          "bi-exclamation-triangle", WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Inventory"}),

        new("open-pos-bills",    "Open POS Bills",           "bi-receipt",        WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),

        new("completed-bills-7d","Completed Bills (7 Days)", "bi-check2-circle",  WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),

        new("items-count",       "Total Items",              "bi-box-seam",       WidgetType.Kpi, 3, 2,
            AllBiz(), AllRoles()),

        new("customers-count",   "Total Customers",          "bi-people",         WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager"}),

        new("employees-count",   "Total Employees",          "bi-person-badge",   WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager"}),

        new("warehouses-count",  "Warehouses",               "bi-building",       WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Inventory"}),

        new("draft-invoices",    "Draft Invoices",           "bi-file-earmark",   WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("sales-7d",          "Sales Last 7 Days",        "bi-graph-up-arrow", WidgetType.Kpi, 3, 2,
            AllBiz(), AllRoles()),

        new("loyalty-members",   "Loyalty Members",          "bi-award",          WidgetType.Kpi, 3, 2,
            new[]{BT.Supermarket, BT.Fashion, BT.ChainStore, BT.Franchise},
            new[]{"Admin","Manager"}),

        new("active-coupons",    "Active Coupons",           "bi-ticket-perforated", WidgetType.Kpi, 3, 2,
            new[]{BT.Supermarket, BT.Fashion, BT.ChainStore, BT.Franchise},
            new[]{"Admin","Manager"}),

        new("returns-pending",   "Pending Returns",          "bi-arrow-return-left", WidgetType.Kpi, 3, 2,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),

        // ───── Charts (wider) ─────
        new("sales-purchases-chart", "Sales vs Purchases (Daily)", "bi-bar-chart-line", WidgetType.Chart, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("monthly-sales-trend", "Monthly Sales Trend (12M)", "bi-graph-up-arrow", WidgetType.Chart, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("sales-channel-mix", "Sales Mix: Invoice vs POS", "bi-pie-chart-fill", WidgetType.Chart, 4, 4,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("weekday-sales-trend", "Sales by Weekday", "bi-calendar-week", WidgetType.Chart, 4, 4,
            AllBiz(), new[]{"Admin","Manager","Cashier","Finance"}),

        new("pos-hourly-chart",  "POS Sales by Hour",        "bi-clock-history",  WidgetType.Chart, 6, 4,
            new[]{BT.Kirana, BT.Supermarket, BT.Restaurant, BT.ChainStore, BT.Franchise},
            new[]{"Admin","Manager","Cashier"}),

        new("category-pie",      "Sales by Category",        "bi-pie-chart",      WidgetType.Chart, 4, 4,
            AllBiz(), new[]{"Admin","Manager"}),

        new("payment-method-pie","Payments by Method",       "bi-credit-card-2-front", WidgetType.Chart, 4, 4,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("top-items-bar",     "Top 10 Selling Items",     "bi-trophy",         WidgetType.Chart, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Inventory"}),

        // ───── Tables / Lists ─────
        new("low-stock-list",    "Low Stock Alerts",         "bi-exclamation-triangle", WidgetType.Table, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Inventory"}),

        new("recent-invoices",   "Recent Invoices",          "bi-receipt-cutoff", WidgetType.Table, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Finance"}),

        new("recent-pos-bills",  "Recent POS Bills",         "bi-receipt",        WidgetType.Table, 6, 4,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),

        new("expiring-items",    "Expiring Items (30 Days)", "bi-calendar-x",     WidgetType.Table, 6, 4,
            new[]{BT.Pharmacy, BT.Kirana, BT.Supermarket, BT.Restaurant},
            new[]{"Admin","Manager","Inventory"}),

        new("eod-summary",       "EOD Summary (Today)",      "bi-journal-check",  WidgetType.Table, 6, 3,
            AllBiz(), new[]{"Admin","Manager","Cashier"}),
    };

    // ── Default layouts per business type ──
    public static List<WidgetPlacement> GetDefaultLayout(BusinessType biz, string role)
    {
        // Filter catalog to widgets compatible with this business + role
        var available = All.Where(w =>
            w.BusinessTypes.Contains(biz) && w.Roles.Contains(role))
            .ToList();

        // Lay out top KPI cards in a row of 4, then charts, then tables
        var kpis = available.Where(w => w.Type == WidgetType.Kpi).Take(4).ToList();
        var charts = available.Where(w => w.Type == WidgetType.Chart).Take(3).ToList();
        var tables = available.Where(w => w.Type == WidgetType.Table).Take(2).ToList();

        var placements = new List<WidgetPlacement>();
        int x = 0, y = 0;

        foreach (var kpi in kpis)
        {
            placements.Add(new WidgetPlacement(kpi.Id, x, y, kpi.DefaultW, kpi.DefaultH));
            x += kpi.DefaultW;
            if (x >= 12) { x = 0; y += kpi.DefaultH; }
        }

        if (x > 0) { x = 0; y += kpis.First().DefaultH; }

        foreach (var chart in charts)
        {
            placements.Add(new WidgetPlacement(chart.Id, x, y, chart.DefaultW, chart.DefaultH));
            x += chart.DefaultW;
            if (x >= 12) { x = 0; y += chart.DefaultH; }
        }

        if (x > 0) { x = 0; y += charts.FirstOrDefault()?.DefaultH ?? 0; }

        foreach (var table in tables)
        {
            placements.Add(new WidgetPlacement(table.Id, x, y, table.DefaultW, table.DefaultH));
            x += table.DefaultW;
            if (x >= 12) { x = 0; y += table.DefaultH; }
        }

        return placements;
    }

    // Helpers
    private static BusinessType[] AllBiz() =>
        Enum.GetValues<BusinessType>();

    private static string[] AllRoles() =>
        new[] { "Admin", "Manager", "Cashier", "Inventory", "Finance" };

    // Alias for brevity
    private static class BT
    {
        public const BusinessType Kirana = BusinessType.Kirana;
        public const BusinessType Supermarket = BusinessType.Supermarket;
        public const BusinessType Hardware = BusinessType.Hardware;
        public const BusinessType Pharmacy = BusinessType.Pharmacy;
        public const BusinessType Fashion = BusinessType.Fashion;
        public const BusinessType Restaurant = BusinessType.Restaurant;
        public const BusinessType ChainStore = BusinessType.ChainStore;
        public const BusinessType Franchise = BusinessType.Franchise;
    }
}

// ── Supporting records ──

public enum WidgetType { Kpi, Chart, Table }

public sealed record WidgetDefinition(
    string Id,
    string Title,
    string Icon,
    WidgetType Type,
    int DefaultW,
    int DefaultH,
    BusinessType[] BusinessTypes,
    string[] Roles);

public sealed record WidgetPlacement(
    string WidgetId,
    int X, int Y,
    int W, int H,
    bool Visible = true);

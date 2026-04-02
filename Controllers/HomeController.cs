using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Data.Identity;
using RetailERP.Models;
using RetailERP.Services;

namespace RetailERP.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly DashboardService _dash;
    private readonly UserManager<ApplicationUser> _userMgr;

    public HomeController(ApplicationDbContext db, DashboardService dash, UserManager<ApplicationUser> userMgr)
    {
        _db = db;
        _dash = dash;
        _userMgr = userMgr;
    }

    // Entry: anonymous -> Landing, logged-in -> Dashboard
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(Dashboard));

        return RedirectToAction(nameof(Landing));
    }

    // Public landing page (before login)
    [AllowAnonymous]
    public async Task<IActionResult> Landing()
    {
        ViewData["Title"] = "Welcome";
        var hasAnyUser = await _db.Users.AsNoTracking().AnyAsync();
        ViewBag.RegistrationOpen = !hasAnyUser;
        return View();
    }

    // Real dashboard (after login) — Sprint 3: all data loaded via AJAX widgets
    // Sprint 4.1: SuperAdmin gets a dedicated platform dashboard
    [Authorize]
    public IActionResult Dashboard()
    {
        ViewData["Title"] = "Dashboard";

        if (User.IsInRole("SuperAdmin"))
            return RedirectToAction(nameof(PlatformDashboard));

        return View();
    }

    // ═══════════════════════════════════════════════════════════
    // Sprint 4.1 – SuperAdmin Platform Dashboard
    // ═══════════════════════════════════════════════════════════

    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> PlatformDashboard(string? search, string? status, string? bizType, int page = 1)
    {
        ViewData["Title"] = "Platform Dashboard";
        const int pageSize = 15;

        // ── KPI counts (lightweight, no confidential data) ──
        var allCompanies = await _db.Companies.AsNoTracking().ToListAsync();
        var totalCompanies = allCompanies.Count;
        var activeCompanies = allCompanies.Count(c => c.IsActive);

        var totalUsers = await _db.Users.AsNoTracking().CountAsync();
        var activeUsers = await _db.Users.AsNoTracking().CountAsync(u => u.IsActive);

        var totalStores = await _db.Stores.IgnoreQueryFilters().AsNoTracking().CountAsync();

        // ── Company list with search / filter / pagination ──
        IQueryable<Company> query = _db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term)
                                   || c.Code.ToLower().Contains(term)
                                   || (c.Email != null && c.Email.ToLower().Contains(term))
                                   || (c.City != null && c.City.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "active") query = query.Where(c => c.IsActive);
            else if (status == "inactive") query = query.Where(c => !c.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(bizType) && Enum.TryParse<BusinessType>(bizType, true, out var bt))
            query = query.Where(c => c.BusinessType == bt);

        var filteredCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(filteredCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var pagedCompanies = await query
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Per-company counts using set-based queries (avoids N+1 count queries per row).
        var pageCompanyIds = pagedCompanies.Select(c => c.CompanyId).ToList();
        var userCounts = await _db.Users.AsNoTracking()
            .Where(u => u.CompanyId.HasValue && pageCompanyIds.Contains(u.CompanyId.Value))
            .GroupBy(u => u.CompanyId!.Value)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count);
        var storeCounts = await _db.Stores.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.CompanyId.HasValue && pageCompanyIds.Contains(s.CompanyId.Value))
            .GroupBy(s => s.CompanyId!.Value)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count);

        var companyRows = pagedCompanies.Select(c => new CompanyRowVm
        {
            CompanyId = c.CompanyId,
            Code = c.Code,
            Name = c.Name,
            BusinessType = c.BusinessType.ToString(),
            IsActive = c.IsActive,
            UserCount = userCounts.GetValueOrDefault(c.CompanyId),
            StoreCount = storeCounts.GetValueOrDefault(c.CompanyId),
            City = c.City,
            State = c.State,
            Phone = c.Phone,
            CreatedAt = c.CreatedAtUtc
        }).ToList();

        // Business-type distribution (for chart)
        var bizTypeDistribution = allCompanies
            .GroupBy(c => c.BusinessType)
            .Select(g => new KeyValuePair<string, int>(g.Key.ToString(), g.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        // Monthly new-company registrations (last 6 months — platform growth, not revenue)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var monthlyRegistrations = allCompanies
            .Where(c => c.CreatedAtUtc >= sixMonthsAgo)
            .GroupBy(c => new { c.CreatedAtUtc.Year, c.CreatedAtUtc.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        // Recent 5 companies
        var recentCompanies = allCompanies.OrderByDescending(c => c.CreatedAtUtc).Take(5).ToList();

        var vm = new PlatformDashboardVm
        {
            TotalCompanies = totalCompanies,
            ActiveCompanies = activeCompanies,
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalStores = totalStores,
            CompanyRows = companyRows,
            FilteredCount = filteredCount,
            Page = page,
            TotalPages = totalPages,
            Search = search,
            StatusFilter = status,
            BizTypeFilter = bizType,
            BizTypeDistribution = bizTypeDistribution,
            MonthlyLabels = monthlyRegistrations.Select(m => $"{m.Year}-{m.Month:D2}").ToList(),
            MonthlyRegistrations = monthlyRegistrations.Select(m => m.Count).ToList(),
            RecentCompanies = recentCompanies
        };

        return View(vm);
    }

    public sealed class PlatformDashboardVm
    {
        // KPIs — platform-level only, no confidential tenant data
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalStores { get; set; }

        // Company table with pagination
        public List<CompanyRowVm> CompanyRows { get; set; } = new();
        public int FilteredCount { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public string? Search { get; set; }
        public string? StatusFilter { get; set; }
        public string? BizTypeFilter { get; set; }

        // Charts — non-confidential
        public List<KeyValuePair<string, int>> BizTypeDistribution { get; set; } = new();
        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlyRegistrations { get; set; } = new();

        public List<Company> RecentCompanies { get; set; } = new();
    }

    public sealed class CompanyRowVm
    {
        public Guid CompanyId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string BusinessType { get; set; } = "";
        public bool IsActive { get; set; }
        public int UserCount { get; set; }
        public int StoreCount { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // Sprint 3 – Dashboard widget API endpoints
    // ═══════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetLayout()
    {
        var user = await _userMgr.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = (await _userMgr.GetRolesAsync(user))
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roles.Count == 0)
            roles.Add("Admin");

        // Use deterministic role priority so default-layout selection is stable.
        var primaryRole = GetPrimaryDashboardRole(roles);
        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

        // Resolve business type deterministically and company-aware.
        var biz = await ResolveDashboardBusinessTypeAsync(user);

        var layout = await _dash.GetLayoutAsync(user.Id, biz, primaryRole);

        // Build catalog across all assigned roles to avoid random widget drops.
        var catalog = DashboardWidgetCatalog.All
            .Where(w => w.BusinessTypes.Contains(biz) && w.Roles.Any(r => roleSet.Contains(r)))
            .Select(w => new { w.Id, w.Title, w.Icon, type = w.Type.ToString(), w.DefaultW, w.DefaultH })
            .ToList();

        return Json(new { layout, catalog });
    }

    private static string GetPrimaryDashboardRole(IEnumerable<string> roles)
    {
        string[] priority = { "SuperAdmin", "Admin", "Manager", "Finance", "Inventory", "Cashier", "User" };
        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);

        foreach (var role in priority)
        {
            if (roleSet.Contains(role))
                return role;
        }

        return roles.FirstOrDefault() ?? "Admin";
    }

    private async Task<BusinessType> ResolveDashboardBusinessTypeAsync(ApplicationUser user)
    {
        if (user.CompanyId.HasValue)
        {
            var companyBiz = await _db.Companies
                .AsNoTracking()
                .Where(c => c.CompanyId == user.CompanyId.Value)
                .Select(c => (BusinessType?)c.BusinessType)
                .FirstOrDefaultAsync();

            if (companyBiz.HasValue)
                return companyBiz.Value;

            var storeBiz = await _db.Stores
                .AsNoTracking()
                .Where(s => s.IsActive && s.CompanyId == user.CompanyId.Value)
                .OrderBy(s => s.CreatedAtUtc)
                .Select(s => (BusinessType?)s.BusinessType)
                .FirstOrDefaultAsync();

            if (storeBiz.HasValue)
                return storeBiz.Value;
        }

        var fallbackBiz = await _db.Stores
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => (BusinessType?)s.BusinessType)
            .FirstOrDefaultAsync();

        return fallbackBiz ?? BusinessType.Other;
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout([FromBody] List<WidgetPlacement>? placements)
    {
        var user = await _userMgr.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (placements is null)
            return BadRequest("Invalid dashboard layout payload.");

        await _dash.SaveLayoutAsync(user.Id, placements);
        return Ok();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetLayout()
    {
        var user = await _userMgr.GetUserAsync(User);
        if (user is null) return Unauthorized();

        await _dash.ResetLayoutAsync(user.Id);
        return Ok();
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> WidgetData(string id, int monthOffset = 0)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        monthOffset = Math.Clamp(monthOffset, -24, 0);
        var data = await _dash.GetWidgetDataAsync(id, monthOffset);
        return Json(data);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ExportMonthlyData(int monthOffset = -1, string section = "all")
    {
        monthOffset = Math.Clamp(monthOffset, -24, 0);
        var normalizedSection = NormalizeExportSection(section);

        var targetMonth = DateTime.Today.AddMonths(monthOffset);
        var monthStart = new DateTime(targetMonth.Year, targetMonth.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var html = await BuildMonthlyExportHtmlAsync(monthStart, monthEnd, normalizedSection);
        var bytes = Encoding.UTF8.GetBytes(html);
        var fileName = $"RetailERP_{normalizedSection}_{monthStart:yyyy_MM}.xls";

        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    private static string NormalizeExportSection(string? section)
    {
        var key = (section ?? "all").Trim().ToLowerInvariant();
        return key switch
        {
            "all" => "all",
            "sales" => "sales",
            "pos" => "pos",
            "purchases" => "purchases",
            "returns" => "returns",
            "payments" => "payments",
            "inventory" => "inventory",
            _ => "all"
        };
    }

    private async Task<string> BuildMonthlyExportHtmlAsync(DateTime monthStart, DateTime monthEnd, string section)
    {
        var includeAll = section == "all";
        var sb = new StringBuilder();

        sb.AppendLine("<html><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;font-size:12px;color:#111827;padding:16px;}");
        sb.AppendLine("h2{margin:0 0 4px 0;} h3{margin:20px 0 6px 0;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:14px;}");
        sb.AppendLine("th,td{border:1px solid #d1d5db;padding:6px 8px;text-align:left;vertical-align:top;}");
        sb.AppendLine("th{background:#f3f4f6;font-weight:600;} .meta{color:#4b5563;margin:2px 0;} .empty{color:#6b7280;font-style:italic;}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h2>RetailERP Monthly Export - {WebUtility.HtmlEncode(monthStart.ToString("MMMM yyyy"))}</h2>");
        sb.AppendLine($"<div class=\"meta\">Period: {WebUtility.HtmlEncode(monthStart.ToString("dd-MMM-yyyy"))} to {WebUtility.HtmlEncode(monthEnd.AddDays(-1).ToString("dd-MMM-yyyy"))}</div>");
        sb.AppendLine($"<div class=\"meta\">Generated UTC: {WebUtility.HtmlEncode(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))}</div>");

        if (includeAll || section == "sales")
        {
            var invoices = await _db.Invoices.AsNoTracking()
                .Where(x => x.InvoiceDate >= monthStart && x.InvoiceDate < monthEnd)
                .OrderBy(x => x.InvoiceDate).ThenBy(x => x.InvoiceNo)
                .Select(x => new
                {
                    x.InvoiceNo,
                    x.InvoiceDate,
                    Customer = x.Customer != null ? x.Customer.Name : "-",
                    Warehouse = x.Warehouse != null ? x.Warehouse.Name : "-",
                    x.TotalAmount,
                    x.Status
                })
                .ToListAsync();

            AppendTable(
                sb,
                "Sales Invoices",
                new[] { "Invoice No", "Date", "Customer", "Warehouse", "Amount", "Status" },
                invoices.Select(x => new[]
                {
                    x.InvoiceNo,
                    x.InvoiceDate.ToString("dd-MMM-yyyy"),
                    x.Customer,
                    x.Warehouse,
                    x.TotalAmount.ToString("0.00"),
                    x.Status == 2 ? "Posted" : "Draft"
                }).ToList());
        }

        if (includeAll || section == "pos")
        {
            var posBills = await _db.PosBills.AsNoTracking()
                .Where(x => x.BillDate >= monthStart && x.BillDate < monthEnd)
                .OrderBy(x => x.BillDate).ThenBy(x => x.BillNo)
                .Select(x => new
                {
                    x.BillNo,
                    x.BillDate,
                    Customer = x.Customer != null ? x.Customer.Name : "Walk-in",
                    Store = x.Store != null ? x.Store.Name : "-",
                    x.GrandTotal,
                    x.Status
                })
                .ToListAsync();

            AppendTable(
                sb,
                "POS Bills",
                new[] { "Bill No", "Date", "Customer", "Store", "Grand Total", "Status" },
                posBills.Select(x => new[]
                {
                    x.BillNo,
                    x.BillDate.ToString("dd-MMM-yyyy"),
                    x.Customer,
                    x.Store,
                    x.GrandTotal.ToString("0.00"),
                    x.Status switch { 1 => "Open", 2 => "Completed", 3 => "Cancelled", 4 => "On Hold", _ => x.Status.ToString() }
                }).ToList());
        }

        if (includeAll || section == "purchases")
        {
            var purchases = await _db.Purchases.AsNoTracking()
                .Where(x => x.PurchaseDate >= monthStart && x.PurchaseDate < monthEnd)
                .OrderBy(x => x.PurchaseDate).ThenBy(x => x.PurchaseNo)
                .Select(x => new
                {
                    x.PurchaseNo,
                    x.PurchaseDate,
                    Supplier = x.Supplier != null ? x.Supplier.Name : "-",
                    Warehouse = x.Warehouse != null ? x.Warehouse.Name : "-",
                    x.TotalAmount,
                    x.Status
                })
                .ToListAsync();

            AppendTable(
                sb,
                "Purchases",
                new[] { "Purchase No", "Date", "Supplier", "Warehouse", "Amount", "Status" },
                purchases.Select(x => new[]
                {
                    x.PurchaseNo,
                    x.PurchaseDate.ToString("dd-MMM-yyyy"),
                    x.Supplier,
                    x.Warehouse,
                    x.TotalAmount.ToString("0.00"),
                    x.Status == 2 ? "Received" : "Draft"
                }).ToList());
        }

        if (includeAll || section == "returns")
        {
            var returns = await _db.PosReturns.AsNoTracking()
                .Where(x => x.ReturnDate >= monthStart && x.ReturnDate < monthEnd)
                .OrderBy(x => x.ReturnDate).ThenBy(x => x.ReturnNo)
                .Select(x => new
                {
                    x.ReturnNo,
                    x.ReturnDate,
                    Customer = x.Customer != null ? x.Customer.Name : "Walk-in",
                    OriginalBill = x.OriginalBill != null ? x.OriginalBill.BillNo : "-",
                    x.TotalRefund,
                    x.Status
                })
                .ToListAsync();

            AppendTable(
                sb,
                "Returns",
                new[] { "Return No", "Date", "Customer", "Original Bill", "Refund", "Status" },
                returns.Select(x => new[]
                {
                    x.ReturnNo,
                    x.ReturnDate.ToString("dd-MMM-yyyy"),
                    x.Customer,
                    x.OriginalBill,
                    x.TotalRefund.ToString("0.00"),
                    x.Status == 2 ? "Processed" : "Pending"
                }).ToList());
        }

        if (includeAll || section == "payments")
        {
            var fromUtc = DateTime.SpecifyKind(monthStart, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(monthEnd, DateTimeKind.Utc);

            var payments = await _db.Payments.AsNoTracking()
                .Where(x => x.PaidAtUtc >= fromUtc && x.PaidAtUtc < toUtc)
                .OrderBy(x => x.PaidAtUtc)
                .Select(x => new
                {
                    x.PaidAtUtc,
                    x.Method,
                    x.Amount,
                    x.IsRefund,
                    x.Reference,
                    BillNo = x.PosBill != null ? x.PosBill.BillNo : "-",
                    InvoiceNo = x.Invoice != null ? x.Invoice.InvoiceNo : "-"
                })
                .ToListAsync();

            AppendTable(
                sb,
                "Payments",
                new[] { "Paid At (UTC)", "Method", "Amount", "Refund", "Reference", "POS Bill", "Invoice" },
                payments.Select(x => new[]
                {
                    x.PaidAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    x.Method,
                    x.Amount.ToString("0.00"),
                    x.IsRefund ? "Yes" : "No",
                    x.Reference ?? "-",
                    x.BillNo,
                    x.InvoiceNo
                }).ToList());
        }

        if (includeAll || section == "inventory")
        {
            var fromUtc = DateTime.SpecifyKind(monthStart, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(monthEnd, DateTimeKind.Utc);

            var stockTx = await _db.StockTransactions.AsNoTracking()
                .Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
                .OrderBy(x => x.OccurredAtUtc)
                .Select(x => new
                {
                    x.OccurredAtUtc,
                    x.Type,
                    Item = x.Item != null ? x.Item.Name : "-",
                    Warehouse = x.Warehouse != null ? x.Warehouse.Name : "-",
                    x.Qty,
                    x.RefType,
                    x.RefId
                })
                .ToListAsync();

            AppendTable(
                sb,
                "Inventory Transactions",
                new[] { "Occurred At (UTC)", "Type", "Item", "Warehouse", "Qty", "Ref Type", "Ref Id" },
                stockTx.Select(x => new[]
                {
                    x.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    x.Type,
                    x.Item,
                    x.Warehouse,
                    x.Qty.ToString("0.##"),
                    x.RefType ?? "-",
                    x.RefId ?? "-"
                }).ToList());
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendTable(StringBuilder sb, string title, string[] headers, List<string[]> rows)
    {
        sb.AppendLine($"<h3>{WebUtility.HtmlEncode(title)}</h3>");

        if (rows.Count == 0)
        {
            sb.AppendLine("<div class=\"empty\">No data for this section in the selected month.</div>");
            return;
        }

        sb.AppendLine("<table><thead><tr>");
        foreach (var header in headers)
            sb.AppendLine($"<th>{WebUtility.HtmlEncode(header)}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.AppendLine("<tr>");
            foreach (var cell in row)
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(cell)}</td>");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
    }

    [AllowAnonymous]
    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var vm = new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            StatusCode = 500,
            Title = "Something went wrong",
            Message = "We could not process your request right now. Please try again."
        };

        if (feature?.Error is not null)
            HttpContext.Items["UnhandledExceptionType"] = feature.Error.GetType().Name;

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(vm);
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("Home/HttpStatus/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpStatus(int statusCode)
    {
        var (title, message) = statusCode switch
        {
            404 => ("Page not found", "The page you requested does not exist or was moved."),
            403 => ("Access denied", "You do not have permission to access this page."),
            401 => ("Unauthorized", "Please sign in to continue."),
            _ => ("Request could not be completed", "Please try again or contact support if this continues.")
        };

        var vm = new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            StatusCode = statusCode,
            Title = title,
            Message = message
        };

        Response.StatusCode = statusCode;
        return View("StatusCode", vm);
    }
}

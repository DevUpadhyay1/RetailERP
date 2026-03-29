using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        var roles = await _userMgr.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Admin";

        // Determine business type from first active store, or default
        var biz = await _db.Stores.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => s.BusinessType)
            .FirstOrDefaultAsync();

        var layout = await _dash.GetLayoutAsync(user.Id, biz, role);

        // Build catalog of widgets available to this user
        var catalog = DashboardWidgetCatalog.All
            .Where(w => w.BusinessTypes.Contains(biz) && w.Roles.Contains(role))
            .Select(w => new { w.Id, w.Title, w.Icon, type = w.Type.ToString(), w.DefaultW, w.DefaultH })
            .ToList();

        return Json(new { layout, catalog });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout([FromBody] List<WidgetPlacement> placements)
    {
        var user = await _userMgr.GetUserAsync(User);
        if (user is null) return Unauthorized();

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
    public async Task<IActionResult> WidgetData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest();
        var data = await _dash.GetWidgetDataAsync(id);
        return Json(data);
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

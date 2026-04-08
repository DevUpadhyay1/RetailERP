using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace RetailERP.Controllers;

[Authorize(Roles = "SuperAdmin,Admin,Manager")]
public class AuditController : Controller
{
    private readonly ApplicationDbContext _db;
    public AuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditFilterVm filter)
    {
        filter ??= new AuditFilterVm();
        NormalizeFilter(filter);

        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var scopeCompanyId = ResolveScopeCompanyId();
        if (!isSuperAdmin && !scopeCompanyId.HasValue)
            return Forbid();

        if (!isSuperAdmin && scopeCompanyId.HasValue)
            filter.CompanyId = scopeCompanyId;

        var (fromUtc, toUtcExclusive) = ResolveRange(filter);

        var query = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);

        if (toUtcExclusive.HasValue)
            query = query.Where(x => x.CreatedAtUtc < toUtcExclusive.Value);

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(x => x.Action == filter.Action);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(x => x.EntityType == filter.EntityType);

        if (!string.IsNullOrWhiteSpace(filter.Actor))
            query = query.Where(x => x.ActorEmail != null && x.ActorEmail.Contains(filter.Actor));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search;
            query = query.Where(x =>
                x.Action.Contains(search) ||
                x.EntityType.Contains(search) ||
                (x.EntityId != null && x.EntityId.Contains(search)) ||
                (x.ActorEmail != null && x.ActorEmail.Contains(search)) ||
                (x.DataJson != null && x.DataJson.Contains(search)));
        }

        const int scanLimit = 5000;
        const int defaultDisplayLimit = 500;
        var effectiveDisplayLimit = filter.ShowAll ? scanLimit : defaultDisplayLimit;

        var logs = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(scanLimit)
            .ToListAsync();

        var companyLookup = await BuildCompanyLookupAsync(isSuperAdmin, scopeCompanyId);
        var actorCompanyLookup = await BuildActorCompanyLookupAsync(logs);

        var rows = logs
            .Select(x => ProjectRow(x, companyLookup, actorCompanyLookup))
            .ToList();

        var filteredRows = ApplyInMemoryFilters(rows, filter, scopeCompanyId, isSuperAdmin)
            .ToList();

        var vm = new AuditIndexVm
        {
            Filter = filter,
            Rows = filteredRows.Take(effectiveDisplayLimit).ToList(),
            TotalFilteredCount = filteredRows.Count,
            ScannedCount = logs.Count,
            IsScanLimitHit = logs.Count >= scanLimit,
            IsDisplayLimitHit = filteredRows.Count > effectiveDisplayLimit,
            DisplayLimit = effectiveDisplayLimit,
            CompanyFilterLocked = !isSuperAdmin,
            LockedCompanyLabel = scopeCompanyId.HasValue && companyLookup.TryGetValue(scopeCompanyId.Value, out var companyLabel)
                ? companyLabel
                : null,
            ModuleOptions = BuildModuleOptions(filter.Module),
            ActionOptions = BuildTextOptions(rows.Select(x => x.Action), filter.Action),
            EntityTypeOptions = BuildTextOptions(rows.Select(x => x.EntityType), filter.EntityType),
            CompanyOptions = BuildCompanyOptions(companyLookup, filter.CompanyId),
            FranchiseCompanyOptions = BuildCompanyOptions(companyLookup, filter.FranchiseCompanyId)
        };

        return View(vm);
    }

    private static IEnumerable<AuditRowVm> ApplyInMemoryFilters(
        IEnumerable<AuditRowVm> rows,
        AuditFilterVm filter,
        Guid? scopeCompanyId,
        bool isSuperAdmin)
    {
        var query = rows;

        if (!string.IsNullOrWhiteSpace(filter.Module))
            query = query.Where(x => string.Equals(x.Module, filter.Module, StringComparison.OrdinalIgnoreCase));

        if (filter.CompanyId.HasValue)
            query = query.Where(x => x.RelatedCompanyIds.Contains(filter.CompanyId.Value));

        if (filter.FranchiseCompanyId.HasValue)
        {
            query = query.Where(x =>
                x.FranchiseCompanyId == filter.FranchiseCompanyId.Value ||
                x.FranchisorCompanyId == filter.FranchiseCompanyId.Value);
        }

        if (!isSuperAdmin && scopeCompanyId.HasValue)
            query = query.Where(x => x.RelatedCompanyIds.Contains(scopeCompanyId.Value));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(x => x.SearchBlob.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(x => x.CreatedAtUtc);
    }

    private static AuditRowVm ProjectRow(
        Data.Entities.AuditLog log,
        IReadOnlyDictionary<Guid, string> companyLookup,
        IReadOnlyDictionary<Guid, Guid?> actorCompanyLookup)
    {
        var parsed = ParseDataJson(log.DataJson);

        var companyId = parsed.CompanyId;
        var franchisorCompanyId = parsed.FranchisorCompanyId;
        var franchiseCompanyId = parsed.FranchiseCompanyId;

        if (!companyId.HasValue && log.ActorUserId.HasValue && actorCompanyLookup.TryGetValue(log.ActorUserId.Value, out var actorCompanyId))
            companyId = actorCompanyId;

        if (!companyId.HasValue && franchisorCompanyId.HasValue)
            companyId = franchisorCompanyId;

        if (!companyId.HasValue &&
            string.Equals(log.EntityType, "Company", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(log.EntityId, out var companyEntityId))
        {
            companyId = companyEntityId;
        }

        var relatedCompanyIds = new HashSet<Guid>();
        if (companyId.HasValue) relatedCompanyIds.Add(companyId.Value);
        if (franchisorCompanyId.HasValue) relatedCompanyIds.Add(franchisorCompanyId.Value);
        if (franchiseCompanyId.HasValue) relatedCompanyIds.Add(franchiseCompanyId.Value);

        var companyDisplay = companyId.HasValue && companyLookup.TryGetValue(companyId.Value, out var companyName)
            ? companyName
            : (companyId.HasValue ? companyId.Value.ToString() : "-");

        var franchiseDisplay = "-";
        if (franchiseCompanyId.HasValue)
        {
            franchiseDisplay = companyLookup.TryGetValue(franchiseCompanyId.Value, out var franchiseName)
                ? franchiseName
                : franchiseCompanyId.Value.ToString();
        }
        else if (franchisorCompanyId.HasValue)
        {
            franchiseDisplay = companyLookup.TryGetValue(franchisorCompanyId.Value, out var franchisorName)
                ? franchisorName
                : franchisorCompanyId.Value.ToString();
        }

        var module = ResolveModule(log.Action, log.EntityType);
        var searchBlob = string.Join(' ', new[]
        {
            log.Action,
            log.EntityType,
            log.EntityId,
            log.ActorEmail,
            log.DataJson,
            module,
            companyDisplay,
            franchiseDisplay
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new AuditRowVm
        {
            Id = log.Id,
            CreatedAtUtc = log.CreatedAtUtc,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            ActorEmail = log.ActorEmail,
            Module = module,
            CompanyId = companyId,
            FranchisorCompanyId = franchisorCompanyId,
            FranchiseCompanyId = franchiseCompanyId,
            CompanyDisplay = companyDisplay,
            FranchiseDisplay = franchiseDisplay,
            DataSummary = ToSummary(log.DataJson),
            SearchBlob = searchBlob,
            RelatedCompanyIds = relatedCompanyIds
        };
    }

    private async Task<Dictionary<Guid, string>> BuildCompanyLookupAsync(bool isSuperAdmin, Guid? scopeCompanyId)
    {
        var query = _db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AsQueryable();

        if (!isSuperAdmin && scopeCompanyId.HasValue)
        {
            query = query.Where(c => c.CompanyId == scopeCompanyId.Value || c.ParentCompanyId == scopeCompanyId.Value);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new { c.CompanyId, c.Code, c.Name })
            .ToDictionaryAsync(c => c.CompanyId, c => $"{c.Code} - {c.Name}");
    }

    private async Task<Dictionary<Guid, Guid?>> BuildActorCompanyLookupAsync(IEnumerable<Data.Entities.AuditLog> logs)
    {
        var actorIds = logs
            .Where(x => x.ActorUserId.HasValue)
            .Select(x => x.ActorUserId!.Value)
            .Distinct()
            .ToList();

        if (actorIds.Count == 0)
            return new Dictionary<Guid, Guid?>();

        return await _db.Users
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.CompanyId })
            .ToDictionaryAsync(x => x.Id, x => x.CompanyId);
    }

    private static List<SelectListItem> BuildModuleOptions(string? selected)
    {
        var modules = new[]
        {
            "POS",
            "Invoice",
            "Purchase",
            "Stock",
            "Franchise",
            "Company",
            "Search",
            "EOD",
            "Sync",
            "Other"
        };

        var selectedKey = selected ?? string.Empty;
        var options = new List<SelectListItem> { new("All Modules", string.Empty, string.IsNullOrWhiteSpace(selectedKey)) };
        options.AddRange(modules.Select(m => new SelectListItem(m, m, string.Equals(selectedKey, m, StringComparison.OrdinalIgnoreCase))));
        return options;
    }

    private static List<SelectListItem> BuildTextOptions(IEnumerable<string?> source, string? selected)
    {
        var values = source
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Take(200)
            .ToList();

        var selectedValue = selected ?? string.Empty;
        var options = new List<SelectListItem> { new("All", string.Empty, string.IsNullOrWhiteSpace(selectedValue)) };
        options.AddRange(values.Select(v => new SelectListItem(v, v, string.Equals(selectedValue, v, StringComparison.OrdinalIgnoreCase))));
        return options;
    }

    private static List<SelectListItem> BuildCompanyOptions(IReadOnlyDictionary<Guid, string> companies, Guid? selected)
    {
        var options = new List<SelectListItem>
        {
            new("All", string.Empty, !selected.HasValue)
        };

        options.AddRange(companies
            .OrderBy(x => x.Value)
            .Select(x => new SelectListItem(x.Value, x.Key.ToString(), selected.HasValue && x.Key == selected.Value)));

        return options;
    }

    private static (DateTime? FromUtc, DateTime? ToUtcExclusive) ResolveRange(AuditFilterVm filter)
    {
        DateTime? fromUtc = filter.FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? toUtcExclusive = filter.ToDate?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        if (!string.IsNullOrWhiteSpace(filter.Month) &&
            DateOnly.TryParseExact($"{filter.Month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            fromUtc ??= monthDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            toUtcExclusive ??= monthDate.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        }

        return (fromUtc, toUtcExclusive);
    }

    private static void NormalizeFilter(AuditFilterVm filter)
    {
        filter.Month = string.IsNullOrWhiteSpace(filter.Month) ? null : filter.Month.Trim();
        filter.Module = string.IsNullOrWhiteSpace(filter.Module) ? null : filter.Module.Trim();
        filter.Action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim();
        filter.EntityType = string.IsNullOrWhiteSpace(filter.EntityType) ? null : filter.EntityType.Trim();
        filter.Actor = string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim();
        filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
    }

    private Guid? ResolveScopeCompanyId()
    {
        var raw = User.FindFirstValue("CompanyId");
        return Guid.TryParse(raw, out var companyId) ? companyId : null;
    }

    private static string ResolveModule(string action, string entityType)
    {
        var actionText = action ?? string.Empty;
        var entityText = entityType ?? string.Empty;

        if (ContainsAny(actionText, entityText, "pos", "bill")) return "POS";
        if (ContainsAny(actionText, entityText, "invoice")) return "Invoice";
        if (ContainsAny(actionText, entityText, "purchase")) return "Purchase";
        if (ContainsAny(actionText, entityText, "stock")) return "Stock";
        if (ContainsAny(actionText, entityText, "franchise")) return "Franchise";
        if (ContainsAny(actionText, entityText, "company")) return "Company";
        if (ContainsAny(actionText, entityText, "search")) return "Search";
        if (ContainsAny(actionText, entityText, "eod")) return "EOD";
        if (ContainsAny(actionText, entityText, "sync")) return "Sync";
        return "Other";
    }

    private static bool ContainsAny(string action, string entity, params string[] tokens)
    {
        return tokens.Any(t =>
            action.Contains(t, StringComparison.OrdinalIgnoreCase) ||
            entity.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "-";

        var compact = json.Replace("\r", " ").Replace("\n", " ").Trim();
        const int max = 260;
        return compact.Length <= max ? compact : compact[..max] + " ...";
    }

    private static ParsedAuditData ParseDataJson(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return ParsedAuditData.Empty;

        try
        {
            using var doc = JsonDocument.Parse(dataJson);
            var root = doc.RootElement;

            var companyId = FindGuid(root, "CompanyId", "companyId", "TenantCompanyId", "tenantCompanyId");
            var franchisorCompanyId = FindGuid(root, "FranchisorCompanyId", "RequestingCompanyId", "requestingCompanyId", "ParentCompanyId", "parentCompanyId");
            var franchiseCompanyId = FindGuid(root, "FranchiseCompanyId", "MappedOperatorCompanyId", "mappedOperatorCompanyId", "FranchiseeCompanyId", "franchiseeCompanyId");

            return new ParsedAuditData(companyId, franchisorCompanyId, franchiseCompanyId);
        }
        catch
        {
            return ParsedAuditData.Empty;
        }
    }

    private static Guid? FindGuid(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryFindProperty(root, propertyName, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryFindProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public sealed class AuditFilterVm
    {
        public string? Month { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public string? Module { get; set; }
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? FranchiseCompanyId { get; set; }
        public string? Actor { get; set; }
        public string? Search { get; set; }
        public bool ShowAll { get; set; }
    }

    public sealed class AuditIndexVm
    {
        public AuditFilterVm Filter { get; set; } = new();
        public List<AuditRowVm> Rows { get; set; } = new();
        public int TotalFilteredCount { get; set; }
        public int ScannedCount { get; set; }
        public bool IsScanLimitHit { get; set; }
        public bool IsDisplayLimitHit { get; set; }
        public int DisplayLimit { get; set; }
        public bool CompanyFilterLocked { get; set; }
        public string? LockedCompanyLabel { get; set; }
        public List<SelectListItem> ModuleOptions { get; set; } = new();
        public List<SelectListItem> ActionOptions { get; set; } = new();
        public List<SelectListItem> EntityTypeOptions { get; set; } = new();
        public List<SelectListItem> CompanyOptions { get; set; } = new();
        public List<SelectListItem> FranchiseCompanyOptions { get; set; } = new();
    }

    public sealed class AuditRowVm
    {
        public long Id { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? ActorEmail { get; set; }
        public string Module { get; set; } = "Other";
        public Guid? CompanyId { get; set; }
        public Guid? FranchisorCompanyId { get; set; }
        public Guid? FranchiseCompanyId { get; set; }
        public string CompanyDisplay { get; set; } = "-";
        public string FranchiseDisplay { get; set; } = "-";
        public string DataSummary { get; set; } = "-";
        public string SearchBlob { get; set; } = string.Empty;
        public HashSet<Guid> RelatedCompanyIds { get; set; } = new();
    }

    private readonly record struct ParsedAuditData(Guid? CompanyId, Guid? FranchisorCompanyId, Guid? FranchiseCompanyId)
    {
        public static ParsedAuditData Empty => new(null, null, null);
    }
}
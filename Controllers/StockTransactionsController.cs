using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory,Finance")]
public sealed class StockTransactionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public StockTransactionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from = null,
        DateTime? to = null,
        string? type = null,
        Guid? warehouseId = null,
        Guid? itemId = null,
        string? q = null,
        string? sort = null,
        string? dir = null,
        int page = 1,
        int pageSize = 20)
    {
        type = (type ?? "").Trim();
        q = (q ?? "").Trim();
        sort ??= "time";
        dir ??= "desc";
        if (page < 1) page = 1;
        if (pageSize is < 10 or > 200) pageSize = 20;

        var query = _db.StockTransactions
            .AsNoTracking()
            .AsQueryable();

        if (from.HasValue)
        {
            var start = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            query = query.Where(x => x.OccurredAtUtc >= start);
        }

        if (to.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(x => x.OccurredAtUtc < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(x => x.Type == type);

        if (warehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);

        if (itemId.HasValue)
            query = query.Where(x => x.ItemId == itemId.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x =>
                (x.Item != null && (x.Item.SKU.Contains(q) || x.Item.Name.Contains(q))) ||
                (x.Warehouse != null && x.Warehouse.Name.Contains(q)) ||
                (x.RefType != null && x.RefType.Contains(q)) ||
                (x.RefId != null && x.RefId.Contains(q)) ||
                (x.Reason != null && x.Reason.Contains(q))
            );
        }

        var ascending = !string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sort?.ToLowerInvariant() switch
        {
            "type" => ascending
                ? query.OrderBy(x => x.Type).ThenByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.StockTransactionId)
                : query.OrderByDescending(x => x.Type).ThenByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.StockTransactionId),
            "qty" => ascending
                ? query.OrderBy(x => x.Qty).ThenByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.StockTransactionId)
                : query.OrderByDescending(x => x.Qty).ThenByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.StockTransactionId),
            _ => ascending
                ? query.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.StockTransactionId)
                : query.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.StockTransactionId)
        };

        var total = await query.CountAsync();
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RetailERP.Data.Entities.StockTransaction
            {
                StockTransactionId = x.StockTransactionId,
                OccurredAtUtc = x.OccurredAtUtc,
                Type = x.Type,
                ItemId = x.ItemId,
                WarehouseId = x.WarehouseId,
                Qty = x.Qty,
                RefType = x.RefType,
                RefId = x.RefId,
                Reason = x.Reason,
                ActorUserId = x.ActorUserId,
                Item = x.Item == null ? null : new RetailERP.Data.Entities.Item
                {
                    SKU = x.Item.SKU,
                    Name = x.Item.Name
                },
                Warehouse = x.Warehouse == null ? null : new RetailERP.Data.Entities.Warehouse
                {
                    Name = x.Warehouse.Name
                },
                ActorUser = x.ActorUser == null ? null : new RetailERP.Data.Identity.ApplicationUser
                {
                    Email = x.ActorUser.Email
                }
            })
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        var fromRow = total == 0 ? 0 : ((page - 1) * pageSize + 1);
        var toRow = Math.Min(page * pageSize, total);

        var vm = new IndexVm
        {
            From = from,
            To = to,
            Type = type,
            WarehouseId = warehouseId,
            ItemId = itemId,
            Query = q,
            Sort = sort!,
            Dir = dir!,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages < 1 ? 1 : totalPages,
            FromRow = fromRow,
            ToRow = toRow,
            Rows = rows
        };

        await LoadLookupsAsync(vm);
        return View(vm);
    }

    private async Task LoadLookupsAsync(IndexVm vm)
    {
        vm.Types = new List<SelectListItem>
        {
            new("IN", "IN"),
            new("OUT", "OUT"),
            new("ADJUSTMENT", "ADJUSTMENT"),
            new("TRANSFER", "TRANSFER"),
            new("RETURN", "RETURN"),
        };

        vm.Warehouses = await _db.Warehouses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.WarehouseId.ToString()))
            .ToListAsync();

        vm.Items = await _db.Items
            .AsNoTracking()
            .OrderBy(x => x.SKU)
            .Select(x => new SelectListItem(x.SKU + " - " + x.Name, x.ItemId.ToString()))
            .ToListAsync();
    }

    public sealed class IndexVm
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Type { get; set; }
        public Guid? WarehouseId { get; set; }
        public Guid? ItemId { get; set; }
        public string? Query { get; set; }

        public string Sort { get; set; } = "time";
        public string Dir { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages { get; set; } = 1;
        public int FromRow { get; set; }
        public int ToRow { get; set; }

        public List<SelectListItem> Types { get; set; } = new();
        public List<SelectListItem> Warehouses { get; set; } = new();
        public List<SelectListItem> Items { get; set; } = new();

        public List<RetailERP.Data.Entities.StockTransaction> Rows { get; set; } = new();
    }
}

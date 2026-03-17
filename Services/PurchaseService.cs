using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

public sealed class PurchaseService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public PurchaseService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Guid> CreateDraftAsync(Guid supplierId, Guid warehouseId, DateTime purchaseDate, Guid? employeeId)
    {
        var purchase = new Purchase
        {
            PurchaseId = Guid.NewGuid(),
            PurchaseNo = await GeneratePurchaseNoAsync(purchaseDate),
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            PurchaseDate = purchaseDate,
            EmployeeId = employeeId,
            Status = 1,
            TotalAmount = 0
        };

        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync();
        return purchase.PurchaseId;
    }

    public async Task AddLineAsync(Guid purchaseId, Guid itemId, decimal qty, decimal unitCost)
    {
        var purchase = await _db.Purchases
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PurchaseId == purchaseId);

        if (purchase is null) throw new InvalidOperationException("Purchase not found.");
        if (purchase.Status != 1) throw new InvalidOperationException("Only Draft purchases can be edited.");

        var item = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId);

        if (item is null) throw new InvalidOperationException("Item not found.");

        _db.PurchaseLines.Add(new PurchaseLine
        {
            PurchaseLineId = Guid.NewGuid(),
            PurchaseId = purchaseId,
            ItemId = itemId,
            Qty = qty,
            UnitCost = unitCost,
            ItemSkuSnapshot = item.SKU,
            ItemNameSnapshot = item.Name
        });

        await _db.SaveChangesAsync();

        purchase.TotalAmount = await _db.PurchaseLines
            .Where(x => x.PurchaseId == purchaseId)
            .SumAsync(x => x.Qty * x.UnitCost);

        await _db.SaveChangesAsync();
    }

    public async Task RemoveLineAsync(Guid purchaseLineId)
    {
        var line = await _db.PurchaseLines.FirstOrDefaultAsync(x => x.PurchaseLineId == purchaseLineId);
        if (line is null) return;

        var purchaseId = line.PurchaseId;
        _db.PurchaseLines.Remove(line);
        await _db.SaveChangesAsync();

        var purchase = await _db.Purchases.FirstAsync(x => x.PurchaseId == purchaseId);
        purchase.TotalAmount = await _db.PurchaseLines
            .Where(x => x.PurchaseId == purchaseId)
            .SumAsync(x => x.Qty * x.UnitCost);

        await _db.SaveChangesAsync();
    }

    public async Task ReceiveAsync(Guid purchaseId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var purchase = await _db.Purchases
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PurchaseId == purchaseId);

        if (purchase is null) throw new InvalidOperationException("Purchase not found.");
        if (purchase.Status != 1) throw new InvalidOperationException("Purchase already received.");
        if (purchase.Lines.Count == 0) throw new InvalidOperationException("Add at least one line before receiving.");

        var storeId = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.WarehouseId == purchase.WarehouseId)
            .Select(w => w.StoreId)
            .FirstOrDefaultAsync();

        foreach (var line in purchase.Lines)
        {
            var stock = await _db.Stocks.FirstOrDefaultAsync(s =>
                s.ItemId == line.ItemId && s.WarehouseId == purchase.WarehouseId);

            if (stock is null)
            {
                stock = new Stock
                {
                    StockId = Guid.NewGuid(),
                    ItemId = line.ItemId,
                    WarehouseId = purchase.WarehouseId,
                    Quantity = 0
                };
                _db.Stocks.Add(stock);
            }

            stock.Quantity += line.Qty;

            _db.StockTransactions.Add(new StockTransaction
            {
                StockTransactionId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Type = "IN",
                ItemId = line.ItemId,
                WarehouseId = purchase.WarehouseId,
                StoreId = storeId,
                Qty = line.Qty,
                RefType = "Purchase",
                RefId = purchase.PurchaseId.ToString(),
                Reason = "Purchase received",
                UnitCost = line.UnitCost,
                CompanyId = purchase.CompanyId
            });
        }

        purchase.TotalAmount = purchase.Lines.Sum(x => x.Qty * x.UnitCost);
        purchase.Status = 2;
        purchase.ReceivedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        try
        {
            await _audit.LogAsync(
                action: "PurchaseReceived",
                entityType: "Purchase",
                entityId: purchase.PurchaseId.ToString(),
                data: new
                {
                    purchase.PurchaseNo,
                    purchase.SupplierId,
                    purchase.WarehouseId,
                    purchase.TotalAmount,
                    Lines = purchase.Lines.Select(l => new { l.ItemId, l.Qty, l.UnitCost })
                }
            );
        }
        catch
        {
            // MVP: don't break receiving if audit fails
        }
    }

    private async Task<string> GeneratePurchaseNoAsync(DateTime date)
    {
        var year = date.Year;
        var prefix = $"PUR-{year}-";

        var last = await _db.Purchases
            .Where(x => x.PurchaseNo.StartsWith(prefix))
            .OrderByDescending(x => x.PurchaseNo)
            .Select(x => x.PurchaseNo)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var numPart = last.Replace(prefix, "");
            if (int.TryParse(numPart, out var n)) next = n + 1;
        }

        return $"{prefix}{next:0000}";
    }
}

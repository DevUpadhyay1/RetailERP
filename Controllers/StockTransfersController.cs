using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models;
using RetailERP.Services;

namespace RetailERP.Controllers;

[Authorize(Roles = "Admin,Manager,Inventory")]
public class StockTransfersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public StockTransfersController(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new StockTransferVm();
        await PopulateListsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockTransferVm vm)
    {
        if (vm.FromWarehouseId == vm.ToWarehouseId)
            ModelState.AddModelError(nameof(vm.ToWarehouseId), "From and To warehouses must be different.");

        if (!ModelState.IsValid)
        {
            await PopulateListsAsync(vm);
            return View(vm);
        }

        using var tx = await _db.Database.BeginTransactionAsync();

        // Lock-ish: load rows to update in this transaction
        var fromStock = await _db.Stocks
            .FirstOrDefaultAsync(s => s.WarehouseId == vm.FromWarehouseId && s.ItemId == vm.ItemId);

        if (fromStock is null)
        {
            ModelState.AddModelError("", "Source warehouse has no stock row for this item.");
            await PopulateListsAsync(vm);
            return View(vm);
        }

        if (fromStock.Quantity < vm.Qty)
        {
            ModelState.AddModelError(nameof(vm.Qty), "Insufficient stock in source warehouse.");
            await PopulateListsAsync(vm);
            return View(vm);
        }

        var toStock = await _db.Stocks
            .FirstOrDefaultAsync(s => s.WarehouseId == vm.ToWarehouseId && s.ItemId == vm.ItemId);

        if (toStock is null)
        {
            toStock = new Stock
            {
                StockId = Guid.NewGuid(),
                WarehouseId = vm.ToWarehouseId,
                ItemId = vm.ItemId,
                Quantity = 0
            };
            _db.Stocks.Add(toStock);
        }

        fromStock.Quantity -= vm.Qty;
        toStock.Quantity += vm.Qty;

        var transferRefId = Guid.NewGuid().ToString();

        var fromStoreId = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.WarehouseId == vm.FromWarehouseId)
            .Select(w => w.StoreId)
            .FirstOrDefaultAsync();

        var toStoreId = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.WarehouseId == vm.ToWarehouseId)
            .Select(w => w.StoreId)
            .FirstOrDefaultAsync();

        var itemCompanyId = await _db.Items
            .AsNoTracking()
            .Where(i => i.ItemId == vm.ItemId)
            .Select(i => i.CompanyId)
            .FirstOrDefaultAsync();

        _db.StockTransactions.Add(new StockTransaction
        {
            StockTransactionId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Type = "TRANSFER",
            ItemId = vm.ItemId,
            WarehouseId = vm.FromWarehouseId,
            StoreId = fromStoreId,
            Qty = -vm.Qty,
            RefType = "StockTransfer",
            RefId = transferRefId,
            Reason = vm.Reason,
            CompanyId = itemCompanyId
        });

        _db.StockTransactions.Add(new StockTransaction
        {
            StockTransactionId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Type = "TRANSFER",
            ItemId = vm.ItemId,
            WarehouseId = vm.ToWarehouseId,
            StoreId = toStoreId,
            Qty = vm.Qty,
            RefType = "StockTransfer",
            RefId = transferRefId,
            Reason = vm.Reason,
            CompanyId = itemCompanyId
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Audit (single event)
        try
        {
            await _audit.LogAsync(
                action: "StockTransferred",
                entityType: "Stock",
                entityId: $"{vm.FromWarehouseId}->{vm.ToWarehouseId}:{vm.ItemId}",
                data: new
                {
                    CompanyId = itemCompanyId,
                    vm.ItemId,
                    vm.FromWarehouseId,
                    vm.ToWarehouseId,
                    vm.Qty,
                    vm.Reason
                }
            );
        }
        catch { }

        TempData["Ok"] = "Stock transfer completed.";
        return RedirectToAction(nameof(Create));
    }

    private async Task PopulateListsAsync(StockTransferVm vm)
    {
        vm.Warehouses = await _db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Name)
            .Select(w => new SelectListItem(w.Name, w.WarehouseId.ToString()))
            .ToListAsync();

        vm.Items = await _db.Items.AsNoTracking()
            .OrderBy(i => i.SKU)
            .Select(i => new SelectListItem($"{i.SKU} - {i.Name}", i.ItemId.ToString()))
            .ToListAsync();
    }
}
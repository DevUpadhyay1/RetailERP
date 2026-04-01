using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RetailERP.Controllers;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Models;
using RetailERP.Services;

namespace RetailERP.Tests;

public class StockAdjustmentWorkflowUatTests
{
    [Fact]
    public async Task NonAdmin_Request_Then_Admin_Approve_ShouldUpdateStock_AndWriteLedger()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var seed = await SeedStockAsync(db, openingQty: 25m);
        var requesterId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var requester = CreateController(db, requesterId, "Inventory");
        var requestResult = await requester.Adjust(new StockAdjustVm
        {
            StockId = seed.StockId,
            DeltaQty = -7m,
            Reason = "Damaged stock",
            ReturnUrl = null
        });

        var requestRedirect = Assert.IsType<RedirectToActionResult>(requestResult);
        Assert.Equal(nameof(StocksController.AdjustmentRequests), requestRedirect.ActionName);

        var pendingReq = await db.StockAdjustmentRequests.SingleAsync();
        Assert.Equal(seed.StockId, pendingReq.StockId);
        Assert.Equal(-7m, pendingReq.DeltaQty);
        Assert.Equal("Damaged stock", pendingReq.Reason);
        Assert.Equal((byte)1, pendingReq.Status);
        Assert.Equal(requesterId, pendingReq.RequestedByUserId);

        var stockAfterRequest = await db.Stocks.SingleAsync(s => s.StockId == seed.StockId);
        Assert.Equal(25m, stockAfterRequest.Quantity);

        var admin = CreateController(db, adminId, "Admin");
        var approveResult = await admin.ApproveRequest(pendingReq.StockAdjustmentRequestId, "Approved in UAT", null);

        var approveRedirect = Assert.IsType<RedirectToActionResult>(approveResult);
        Assert.Equal(nameof(StocksController.AdjustmentRequests), approveRedirect.ActionName);

        var approvedReq = await db.StockAdjustmentRequests.SingleAsync(r => r.StockAdjustmentRequestId == pendingReq.StockAdjustmentRequestId);
        Assert.Equal((byte)2, approvedReq.Status);
        Assert.Equal(adminId, approvedReq.ReviewedByUserId);
        Assert.NotNull(approvedReq.AppliedStockTransactionId);

        var stockAfterApproval = await db.Stocks.SingleAsync(s => s.StockId == seed.StockId);
        Assert.Equal(18m, stockAfterApproval.Quantity);

        var ledger = await db.StockTransactions.SingleAsync(x =>
            x.RefType == "StockAdjustRequest" &&
            x.RefId == pendingReq.StockAdjustmentRequestId.ToString());

        Assert.Equal("ADJUSTMENT", ledger.Type);
        Assert.Equal(-7m, ledger.Qty);
        Assert.Equal(seed.ItemId, ledger.ItemId);
        Assert.Equal(seed.WarehouseId, ledger.WarehouseId);
        Assert.Equal(adminId, ledger.ActorUserId);
    }

    [Fact]
    public async Task NonAdmin_Request_Then_Admin_Reject_ShouldKeepStockUnchanged_AndWriteNoLedger()
    {
        var options = CreateOptions();
        using var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        var seed = await SeedStockAsync(db, openingQty: 25m);
        var requesterId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var requester = CreateController(db, requesterId, "Manager");
        var requestResult = await requester.Adjust(new StockAdjustVm
        {
            StockId = seed.StockId,
            DeltaQty = 5m,
            Reason = "Cycle-count correction",
            ReturnUrl = null
        });

        Assert.IsType<RedirectToActionResult>(requestResult);
        var pendingReq = await db.StockAdjustmentRequests.SingleAsync();
        Assert.Equal((byte)1, pendingReq.Status);

        var admin = CreateController(db, adminId, "Admin");
        var rejectResult = await admin.RejectRequest(pendingReq.StockAdjustmentRequestId, "Rejected in UAT", null);

        var rejectRedirect = Assert.IsType<RedirectToActionResult>(rejectResult);
        Assert.Equal(nameof(StocksController.AdjustmentRequests), rejectRedirect.ActionName);

        var rejectedReq = await db.StockAdjustmentRequests.SingleAsync(r => r.StockAdjustmentRequestId == pendingReq.StockAdjustmentRequestId);
        Assert.Equal((byte)3, rejectedReq.Status);
        Assert.Equal(adminId, rejectedReq.ReviewedByUserId);

        var stockAfterReject = await db.Stocks.SingleAsync(s => s.StockId == seed.StockId);
        Assert.Equal(25m, stockAfterReject.Quantity);

        var ledgerRows = await db.StockTransactions
            .Where(x => x.RefType == "StockAdjustRequest" && x.RefId == pendingReq.StockAdjustmentRequestId.ToString())
            .CountAsync();
        Assert.Equal(0, ledgerRows);
    }

    private static StocksController CreateController(ApplicationDbContext db, Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };

        var audit = new AuditService(db, new HttpContextAccessor { HttpContext = httpContext });
        var controller = new StocksController(db, audit)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        return controller;
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task<(Guid StockId, Guid ItemId, Guid WarehouseId)> SeedStockAsync(ApplicationDbContext db, decimal openingQty)
    {
        var companyId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var stockId = Guid.NewGuid();

        db.Companies.Add(new Company
        {
            CompanyId = companyId,
            Code = "UAT-C1",
            Name = "UAT Company"
        });

        db.Stores.Add(new Store
        {
            StoreId = storeId,
            StoreCode = "UAT-S1",
            Name = "Main Store",
            CompanyId = companyId,
            IsActive = true
        });

        db.Warehouses.Add(new Warehouse
        {
            WarehouseId = warehouseId,
            Name = "Main Warehouse",
            StoreId = storeId,
            CompanyId = companyId
        });

        db.Items.Add(new Item
        {
            ItemId = itemId,
            SKU = "UAT-ITEM-1",
            Name = "UAT Item",
            UnitPrice = 100m,
            CompanyId = companyId,
            IsActive = true
        });

        db.Stocks.Add(new Stock
        {
            StockId = stockId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            Quantity = openingQty,
            CompanyId = companyId
        });

        await db.SaveChangesAsync();
        return (stockId, itemId, warehouseId);
    }
}
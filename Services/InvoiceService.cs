using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Hubs;
using RetailERP.Services;

namespace RetailERP.Services;

public class InvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly IHubContext<RetailHub> _hub;
    private readonly CacheService _cache;
    private readonly InvoiceNumberingService _numbering;
    private readonly ITenantProvider _tenant;


    public InvoiceService(
        ApplicationDbContext db,
        AuditService audit,
        IHubContext<RetailHub> hub,
        CacheService cache,
        InvoiceNumberingService numbering,
        ITenantProvider tenant)
    {
        _db = db;
        _audit = audit;
        _hub = hub;
        _cache = cache;
        _numbering = numbering;
        _tenant = tenant;
    }
    public async Task<Guid> CreateDraftAsync(
        Guid customerId,
        Guid warehouseId,
        DateTime invoiceDate,
        Guid? employeeId,
        InvoiceDocumentType documentType = InvoiceDocumentType.TaxInvoice,
        DateTime? dueDate = null,
        string? referenceInvoiceNo = null)
    {
        var warehouseMeta = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.WarehouseId == warehouseId)
            .Select(w => new { w.StoreId, w.CompanyId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Warehouse not found.");

        var companyId = _tenant.CompanyId ?? warehouseMeta.CompanyId
            ?? throw new InvalidOperationException("Tenant company context not found for invoice numbering.");

        var nextInvoiceNo = await _numbering.GenerateNextInvoiceNoAsync(
            companyId,
            warehouseMeta.StoreId,
            documentType,
            invoiceDate);

        var preferredTemplateId = await ResolveInvoiceTemplateIdAsync(
            companyId,
            warehouseMeta.StoreId,
            documentType);

        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceNo = nextInvoiceNo,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            DocumentType = documentType,
            ReferenceInvoiceNo = string.IsNullOrWhiteSpace(referenceInvoiceNo) ? null : referenceInvoiceNo.Trim(),
            BillTemplateId = preferredTemplateId,
            EmployeeId = employeeId,
            Status = 1,
            TotalAmount = 0
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice.InvoiceId;
    }

    public async Task AddLineAsync(Guid invoiceId, Guid itemId, decimal qty, decimal unitPrice)
    {
        var invoice = await _db.Invoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);

        if (invoice is null) throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != 1) throw new InvalidOperationException("Only Draft invoices can be edited.");

        var item = await _db.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId);

        if (item is null) throw new InvalidOperationException("Item not found.");

        _db.InvoiceLines.Add(new InvoiceLine
        {
            InvoiceLineId = Guid.NewGuid(),
            InvoiceId = invoiceId,
            ItemId = itemId,
            Qty = qty,
            UnitPrice = unitPrice,
            ItemSkuSnapshot = item.SKU,
            ItemNameSnapshot = item.Name,
            GstPercentSnapshot = item.GstPercent,
            HsnCodeSnapshot = item.HsnCode,
            DiscountAmount = 0
        });

        await _db.SaveChangesAsync();

        invoice.TotalAmount = await _db.InvoiceLines
            .Where(x => x.InvoiceId == invoiceId)
            .SumAsync(x => x.Qty * x.UnitPrice);

        await _db.SaveChangesAsync();
    }

    public async Task RemoveLineAsync(Guid invoiceLineId)
    {
        var line = await _db.InvoiceLines.FirstOrDefaultAsync(x => x.InvoiceLineId == invoiceLineId);
        if (line is null) return;

        var invoiceId = line.InvoiceId;
        _db.InvoiceLines.Remove(line);
        await _db.SaveChangesAsync();

        var invoice = await _db.Invoices.FirstAsync(x => x.InvoiceId == invoiceId);
        invoice.TotalAmount = await _db.InvoiceLines
            .Where(x => x.InvoiceId == invoiceId)
            .SumAsync(x => x.Qty * x.UnitPrice);

        await _db.SaveChangesAsync();
    }

    public async Task PostAsync(Guid invoiceId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var invoice = await _db.Invoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.InvoiceId == invoiceId);

        if (invoice is null) throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != 1) throw new InvalidOperationException("Invoice already posted.");
        if (invoice.Lines.Count == 0) throw new InvalidOperationException("Add at least one line before posting.");

        var storeId = await _db.Warehouses
            .AsNoTracking()
            .Where(w => w.WarehouseId == invoice.WarehouseId)
            .Select(w => w.StoreId)
            .FirstOrDefaultAsync();

        foreach (var line in invoice.Lines)
        {
            var stock = await _db.Stocks.FirstOrDefaultAsync(s =>
                s.ItemId == line.ItemId && s.WarehouseId == invoice.WarehouseId);

            if (stock is null)
                throw new InvalidOperationException("Stock row missing for an item in this warehouse.");

            if (stock.Quantity < line.Qty)
                throw new InvalidOperationException("Insufficient stock for one of the items.");

            stock.Quantity -= line.Qty;

            _db.StockTransactions.Add(new StockTransaction
            {
                StockTransactionId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Type = "OUT",
                ItemId = line.ItemId,
                WarehouseId = invoice.WarehouseId,
                StoreId = storeId,
                Qty = -line.Qty,
                RefType = "Invoice",
                RefId = invoice.InvoiceId.ToString(),
                Reason = "Invoice posted",
                UnitPrice = line.UnitPrice,
                CompanyId = invoice.CompanyId
            });
        }

        invoice.TotalAmount = invoice.Lines.Sum(x => x.Qty * x.UnitPrice);
        invoice.Status = 2;
        invoice.PostedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        try
        {
            await _audit.LogAsync(
                action: "InvoicePosted",
                entityType: "Invoice",
                entityId: invoice.InvoiceId.ToString(),
                data: new
                {
                    CompanyId = invoice.CompanyId,
                    invoice.InvoiceNo,
                    invoice.WarehouseId,
                    invoice.CustomerId,
                    invoice.TotalAmount,
                    Lines = invoice.Lines.Select(l => new { l.ItemId, l.Qty, l.UnitPrice })
                }
            );
        }
        catch
        {
            // MVP: don't break posting if audit fails
        }

        try
        {
            var companyGroup = $"company-{invoice.CompanyId}";
            await _hub.Clients.Group(companyGroup).SendAsync("InvoicePosted", new
            {
                invoiceId = invoice.InvoiceId,
                invoiceNo = invoice.InvoiceNo,
                grandTotal = invoice.TotalAmount,
                customerId = invoice.CustomerId,
                postedAt = invoice.PostedAt
            });
        }
        catch { /* don't break posting if SignalR fails */ }

        // Invalidate Redis cache for relevant dashboard KPIs and charts
        await InvalidateDashboardCacheAsync();
    }

    private async Task InvalidateDashboardCacheAsync()
    {
        var keys = new[]
        {
            "widget:total-sales", "widget:sales-7d", "widget:draft-invoices", "widget:recent-invoices",
            "widget:sales-purchases-chart", "widget:category-pie"
        };
        foreach (var k in keys) await _cache.RemoveAsync(k);
    }

    private async Task<Guid?> ResolveInvoiceTemplateIdAsync(Guid companyId, Guid? storeId, InvoiceDocumentType documentType)
    {
        var templateQuery = _db.BillTemplates
            .AsNoTracking()
            .Where(t =>
                t.CompanyId == companyId &&
                t.TemplateType == 2 &&
                t.DocumentType == documentType);

        BillTemplate? template = null;

        if (storeId.HasValue)
        {
            template = await templateQuery
                .Where(t =>
                    t.TemplateScope == InvoiceTemplateScope.Store &&
                    t.StoreId == storeId.Value &&
                    t.IsDefault)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .FirstOrDefaultAsync();
        }

        template ??= await templateQuery
            .Where(t => t.TemplateScope == InvoiceTemplateScope.Company && t.IsDefault)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        if (storeId.HasValue)
        {
            template ??= await templateQuery
                .Where(t => t.TemplateScope == InvoiceTemplateScope.Store && t.StoreId == storeId.Value)
                .OrderByDescending(t => t.UpdatedAtUtc)
                .FirstOrDefaultAsync();
        }

        template ??= await templateQuery
            .Where(t => t.TemplateScope == InvoiceTemplateScope.Company)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync();

        return template?.BillTemplateId;
    }

}

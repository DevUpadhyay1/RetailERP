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


    public InvoiceService(ApplicationDbContext db, AuditService audit, IHubContext<RetailHub> hub)
    {
        _db = db;
        _audit = audit;
        _hub = hub;
    }
    public Task<Guid> CreateDraftAsync(Guid customerId, Guid warehouseId, DateTime invoiceDate, Guid? employeeId)
    {
        return CreateDraftAsync(
            customerId,
            warehouseId,
            invoiceDate,
            employeeId,
            InvoiceDocumentType.TaxInvoice,
            null,
            null);
    }

    public async Task<Guid> CreateDraftAsync(
        Guid customerId,
        Guid warehouseId,
        DateTime invoiceDate,
        Guid? employeeId,
        InvoiceDocumentType documentType,
        DateTime? dueDate,
        string? referenceInvoiceNo)
    {
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceNo = await GenerateInvoiceNoAsync(invoiceDate),
            CustomerId = customerId,
            WarehouseId = warehouseId,
            InvoiceDate = invoiceDate,
            DocumentType = documentType,
            DueDate = dueDate,
            ReferenceInvoiceNo = string.IsNullOrWhiteSpace(referenceInvoiceNo) ? null : referenceInvoiceNo.Trim(),
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

        // Sprint 9: Broadcast real-time event via SignalR
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
    }

    private async Task<string> GenerateInvoiceNoAsync(DateTime date)
    {
        var year = date.Year;
        var prefix = $"INV-{year}-";

        var last = await _db.Invoices
            .Where(x => x.InvoiceNo.StartsWith(prefix))
            .OrderByDescending(x => x.InvoiceNo)
            .Select(x => x.InvoiceNo)
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
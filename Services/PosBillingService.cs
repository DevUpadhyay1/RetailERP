using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using RetailERP.Data;
using RetailERP.Data.Entities;
using RetailERP.Hubs;

namespace RetailERP.Services;

/// <summary>
/// Phase 3/4/5: POS Billing, Payments, and Returns service.
/// Handles the full DMART cashier workflow: create bill → scan items → pay → complete.
/// </summary>
public class PosBillingService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly LoyaltyService _loyalty;
    private readonly CouponService _coupons;
    private readonly IHubContext<RetailHub> _hub;

    public PosBillingService(ApplicationDbContext db, AuditService audit, LoyaltyService loyalty, CouponService coupons, IHubContext<RetailHub> hub)
    {
        _db = db;
        _audit = audit;
        _loyalty = loyalty;
        _coupons = coupons;
        _hub = hub;
    }

    // ────────────────────────────────────────────────────────
    // Phase 3: POS Bill operations
    // ────────────────────────────────────────────────────────

    /// <summary>Create a new Open POS bill.</summary>
    public async Task<Guid> CreateBillAsync(Guid storeId, Guid warehouseId, Guid? customerId, Guid? cashierUserId)
    {
        var bill = new PosBill
        {
            PosBillId = Guid.NewGuid(),
            BillNo = await GenerateBillNoAsync(),
            StoreId = storeId,
            WarehouseId = warehouseId,
            CustomerId = customerId,
            CashierUserId = cashierUserId,
            BillDate = DateTime.Today,
            Status = 1
        };

        _db.PosBills.Add(bill);
        await _db.SaveChangesAsync();
        return bill.PosBillId;
    }

    /// <summary>Lookup an item by barcode or SKU for the scan input.</summary>
    public async Task<ItemLookupResult?> LookupItemAsync(string code, Guid warehouseId)
    {
        code = code.Trim();
        var item = await _db.Items
            .AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x =>
                (x.Barcode != null && x.Barcode == code) || x.SKU == code);

        if (item is null) return null;

        var stockQty = await _db.Stocks
            .AsNoTracking()
            .Where(s => s.ItemId == item.ItemId && s.WarehouseId == warehouseId)
            .Select(s => s.Quantity)
            .FirstOrDefaultAsync();

        return new ItemLookupResult
        {
            ItemId = item.ItemId,
            SKU = item.SKU,
            Barcode = item.Barcode,
            Name = item.Name,
            UnitPrice = item.UnitPrice,
            MRP = item.MRP,
            GstPercent = item.GstPercent,
            HsnCode = item.HsnCode,
            UnitName = item.Unit?.Name,
            CategoryName = item.Category?.Name,
            StockAvailable = stockQty
        };
    }

    /// <summary>Add a scanned item line to an open bill.</summary>
    public async Task<PosBillLine> AddLineAsync(Guid billId, Guid itemId, decimal qty)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (bill.Status != 1)
            throw new InvalidOperationException("Bill is not open for editing.");

        var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.ItemId == itemId)
            ?? throw new InvalidOperationException("Item not found.");

        // Check if item already on bill — increment qty
        var existing = bill.Lines.FirstOrDefault(l => l.ItemId == itemId);
        if (existing is not null)
        {
            existing.Qty += qty;
            existing.LineTotal = existing.Qty * existing.UnitPrice - existing.DiscountAmount;
            RecalcTotals(bill);
            await _db.SaveChangesAsync();
            return existing;
        }

        var unitPrice = item.MRP ?? item.UnitPrice;
        var line = new PosBillLine
        {
            PosBillLineId = Guid.NewGuid(),
            PosBillId = billId,
            ItemId = itemId,
            BarcodeSnapshot = item.Barcode,
            SkuSnapshot = item.SKU,
            ItemNameSnapshot = item.Name,
            Qty = qty,
            UnitPrice = unitPrice,
            MrpSnapshot = item.MRP,
            GstPercentSnapshot = item.GstPercent,
            HsnCodeSnapshot = item.HsnCode,
            DiscountPercent = 0,
            DiscountAmount = 0,
            NetRate = unitPrice,
            LineTotal = qty * unitPrice
        };

        _db.PosBillLines.Add(line);
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
        return line;
    }

    /// <summary>Update quantity of an existing line.</summary>
    public async Task UpdateLineQtyAsync(Guid billLineId, decimal newQty)
    {
        var line = await _db.PosBillLines.FirstOrDefaultAsync(l => l.PosBillLineId == billLineId)
            ?? throw new InvalidOperationException("Line not found.");

        var bill = await _db.PosBills.Include(b => b.Lines).FirstAsync(b => b.PosBillId == line.PosBillId);
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open for editing.");

        if (newQty <= 0)
        {
            _db.PosBillLines.Remove(line);
            bill.Lines.Remove(line);
        }
        else
        {
            line.Qty = newQty;
            line.LineTotal = newQty * line.UnitPrice - line.DiscountAmount;
        }

        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Remove a line from an open bill.</summary>
    public async Task RemoveLineAsync(Guid billLineId)
    {
        var line = await _db.PosBillLines.FirstOrDefaultAsync(l => l.PosBillLineId == billLineId);
        if (line is null) return;

        var bill = await _db.PosBills.Include(b => b.Lines).FirstAsync(b => b.PosBillId == line.PosBillId);
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open for editing.");

        _db.PosBillLines.Remove(line);
        bill.Lines.Remove(line);
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cancel an open bill (no stock impact).</summary>
    public async Task CancelBillAsync(Guid billId)
    {
        var bill = await _db.PosBills.FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (bill.Status != 1) throw new InvalidOperationException("Only open bills can be cancelled.");

        bill.Status = 3; // Cancelled
        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────
    // Sprint 7: Hold / Unhold bills
    // ────────────────────────────────────────────────────────

    /// <summary>Put an open bill on hold.</summary>
    public async Task HoldBillAsync(Guid billId)
    {
        var bill = await _db.PosBills.FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Only open bills can be put on hold.");
        bill.Status = 4; // OnHold
        await _db.SaveChangesAsync();
    }

    /// <summary>Resume a held bill back to open.</summary>
    public async Task UnholdBillAsync(Guid billId)
    {
        var bill = await _db.PosBills.FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 4) throw new InvalidOperationException("Only held bills can be resumed.");
        bill.Status = 1; // Open
        await _db.SaveChangesAsync();
    }

    /// <summary>Get all held bills for the current tenant (for "Pop Hold Bills" UI).</summary>
    public async Task<List<HeldBillInfo>> GetHeldBillsAsync()
    {
        return await _db.PosBills
            .AsNoTracking()
            .Where(b => b.Status == 4)
            .Include(b => b.Store)
            .Include(b => b.Customer)
            .OrderByDescending(b => b.BillDate)
            .Select(b => new HeldBillInfo
            {
                PosBillId = b.PosBillId,
                BillNo = b.BillNo,
                BillDate = b.BillDate,
                StoreName = b.Store != null ? b.Store.Name : "",
                CustomerName = b.Customer != null ? b.Customer.Name : "Walk-in",
                ItemCount = b.Lines.Count,
                GrandTotal = b.GrandTotal
            })
            .ToListAsync();
    }

    // ────────────────────────────────────────────────────────
    // Sprint 7: Bill-level discount / charge
    // ────────────────────────────────────────────────────────

    /// <summary>Set bill-level additional discount %.</summary>
    public async Task SetAddDiscountAsync(Guid billId, decimal discountPercent)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");
        bill.AddDiscountPercent = Math.Clamp(discountPercent, 0, 100);
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Set bill-level additional charge %.</summary>
    public async Task SetAddChargeAsync(Guid billId, decimal chargePercent)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");
        bill.AddChargePercent = Math.Clamp(chargePercent, 0, 100);
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Apply a manual line-level discount % to a specific line.</summary>
    public async Task SetLineDiscountAsync(Guid billLineId, decimal discountPercent)
    {
        var line = await _db.PosBillLines.FirstOrDefaultAsync(l => l.PosBillLineId == billLineId)
            ?? throw new InvalidOperationException("Line not found.");
        var bill = await _db.PosBills.Include(b => b.Lines).FirstAsync(b => b.PosBillId == line.PosBillId);
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        discountPercent = Math.Clamp(discountPercent, 0, 100);
        line.DiscountPercent = discountPercent;
        line.DiscountAmount = Math.Round(line.Qty * line.UnitPrice * discountPercent / 100m, 2);
        line.NetRate = line.UnitPrice > 0 && discountPercent > 0
            ? Math.Round(line.UnitPrice * (1 - discountPercent / 100m), 2)
            : line.UnitPrice;
        line.LineTotal = line.Qty * line.UnitPrice - line.DiscountAmount;
        line.AppliedPromotionId = null; // manual override clears auto-promo

        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────
    // Phase 6: Loyalty + Coupons on bill
    // ────────────────────────────────────────────────────────

    /// <summary>Attach a loyalty card to an open bill.</summary>
    public async Task AttachLoyaltyCardAsync(Guid billId, Guid loyaltyCardId)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        bill.LoyaltyCardId = loyaltyCardId;
        await _db.SaveChangesAsync();
    }

    /// <summary>Remove loyalty card from bill.</summary>
    public async Task RemoveLoyaltyCardAsync(Guid billId)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        bill.LoyaltyCardId = null;
        bill.LoyaltyPointsRedeemed = 0;
        bill.LoyaltyDiscount = 0;
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Redeem loyalty points on a bill (applies a discount).</summary>
    public async Task RedeemLoyaltyOnBillAsync(Guid billId, decimal points)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");
        if (bill.LoyaltyCardId is null) throw new InvalidOperationException("No loyalty card attached.");

        var discount = points * LoyaltyService.RedeemValuePerPoint;
        bill.LoyaltyPointsRedeemed = points;
        bill.LoyaltyDiscount = discount;
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Apply a coupon to an open bill.</summary>
    public async Task ApplyCouponAsync(Guid billId, string couponCode)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        var result = await _coupons.ValidateAsync(couponCode, bill.SubTotal);
        if (!result.Success) throw new InvalidOperationException(result.Message);

        bill.CouponId = result.CouponId;
        bill.CouponDiscount = result.Discount;
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    /// <summary>Remove coupon from bill.</summary>
    public async Task RemoveCouponAsync(Guid billId)
    {
        var bill = await _db.PosBills.Include(b => b.Lines).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        bill.CouponId = null;
        bill.CouponDiscount = 0;
        RecalcTotals(bill);
        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────
    // Phase 4: Payments + bill completion
    // ────────────────────────────────────────────────────────

    /// <summary>Add a payment to a bill.</summary>
    public async Task<Payment> AddPaymentAsync(Guid billId, string method, decimal amount, string? reference)
    {
        var bill = await _db.PosBills.Include(b => b.Payments).FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            PosBillId = billId,
            Method = method,
            Amount = amount,
            Reference = reference,
            PaidAtUtc = DateTime.UtcNow,
            IsRefund = false
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }

    /// <summary>Remove a payment from an open bill.</summary>
    public async Task RemovePaymentAsync(Guid paymentId)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        if (payment is null) return;
        if (payment.IsRefund)
            throw new InvalidOperationException("Refund entries cannot be removed.");

        var bill = await _db.PosBills.FirstOrDefaultAsync(b => b.PosBillId == payment.PosBillId);
        if (bill is null)
            throw new InvalidOperationException("Bill not found.");
        if (bill.Status != 1)
            throw new InvalidOperationException("Payments can be removed only while bill is open.");

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Complete a POS bill: validate payments total = grand total,
    /// deduct stock, write StockTransactions, mark completed.
    /// </summary>
    public async Task CompleteBillAsync(Guid billId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var bill = await _db.PosBills
            .Include(b => b.Lines)
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.PosBillId == billId)
            ?? throw new InvalidOperationException("Bill not found.");

        if (bill.Status != 1) throw new InvalidOperationException("Bill is not open.");
        if (bill.Lines.Count == 0) throw new InvalidOperationException("Add at least one item.");

        // Validate payments total >= grand total
        var totalPaid = bill.Payments.Where(p => !p.IsRefund).Sum(p => p.Amount);
        if (totalPaid < bill.GrandTotal)
            throw new InvalidOperationException($"Payment shortfall: ₹{bill.GrandTotal - totalPaid:N2} remaining.");

        var storeId = bill.StoreId;

        // Stock validation + FIFO deduction + ledger entries
        foreach (var line in bill.Lines)
        {
            // FIFO: get all stock batches for this item+warehouse, ordered by expiry (soonest first), then oldest first
            var batches = await _db.Stocks
                .Where(s => s.ItemId == line.ItemId && s.WarehouseId == bill.WarehouseId && s.Quantity > 0)
                .OrderBy(s => s.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(s => s.ManufactureDate ?? DateTime.MinValue)
                .ThenBy(s => s.CreatedAtUtc)
                .ToListAsync();

            var totalAvailable = batches.Sum(s => s.Quantity);
            if (batches.Count == 0)
                throw new InvalidOperationException($"No stock record for {line.ItemNameSnapshot} in this warehouse.");
            if (totalAvailable < line.Qty)
                throw new InvalidOperationException($"Insufficient stock for {line.ItemNameSnapshot}: available {totalAvailable}, required {line.Qty}.");

            var remaining = line.Qty;
            foreach (var batch in batches)
            {
                if (remaining <= 0) break;

                var deduct = Math.Min(batch.Quantity, remaining);
                batch.Quantity -= deduct;
                remaining -= deduct;

                _db.StockTransactions.Add(new StockTransaction
                {
                    StockTransactionId = Guid.NewGuid(),
                    OccurredAtUtc = DateTime.UtcNow,
                    Type = "OUT",
                    ItemId = line.ItemId,
                    WarehouseId = bill.WarehouseId,
                    StoreId = storeId,
                    Qty = -deduct,
                    RefType = "PosBill",
                    RefId = bill.PosBillId.ToString(),
                    Reason = $"POS Bill {bill.BillNo}" + (batch.BatchNumber != null ? $" [Batch: {batch.BatchNumber}]" : ""),
                    UnitPrice = line.UnitPrice,
                    CompanyId = bill.CompanyId
                });
            }
        }

        bill.Status = 2; // Completed
        bill.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Phase 6: Post-completion — earn loyalty points + record coupon usage
        try
        {
            if (bill.LoyaltyCardId.HasValue)
            {
                // Redeem points (deduct from card) if the bill had loyalty redemption
                if (bill.LoyaltyPointsRedeemed > 0)
                {
                    await _loyalty.RedeemPointsAsync(bill.LoyaltyCardId.Value, bill.PosBillId, bill.LoyaltyPointsRedeemed);
                }

                // Earn points on the net bill amount (after discounts)
                var earnableAmount = bill.GrandTotal;
                await _loyalty.EarnPointsAsync(bill.LoyaltyCardId.Value, bill.PosBillId, earnableAmount);
            }

            if (bill.CouponId.HasValue && bill.CouponDiscount > 0)
            {
                await _coupons.RecordUsageAsync(bill.CouponId.Value, bill.PosBillId, bill.CouponDiscount);
            }
        }
        catch { /* don't break billing if loyalty/coupon fails */ }

        try
        {
            await _audit.LogAsync(
                action: "PosBillCompleted",
                entityType: "PosBill",
                entityId: bill.PosBillId.ToString(),
                data: new
                {
                    bill.BillNo,
                    bill.StoreId,
                    bill.WarehouseId,
                    bill.GrandTotal,
                    LineCount = bill.Lines.Count,
                    PaymentCount = bill.Payments.Count
                });
        }
        catch { /* don't break billing if audit fails */ }

        // Sprint 9: Broadcast real-time event via SignalR
        try
        {
            var companyGroup = $"company-{bill.CompanyId}";
            await _hub.Clients.Group(companyGroup).SendAsync("BillCompleted", new
            {
                billId = bill.PosBillId,
                billNo = bill.BillNo,
                grandTotal = bill.GrandTotal,
                storeId = bill.StoreId,
                lineCount = bill.Lines.Count,
                completedAt = bill.CompletedAtUtc
            });
        }
        catch { /* don't break billing if SignalR fails */ }
    }

    // ────────────────────────────────────────────────────────
    // Phase 5: Returns & Refunds
    // ────────────────────────────────────────────────────────

    /// <summary>Process a POS return: create return record, reverse stock, add refund payment.</summary>
    public async Task<Guid> ProcessReturnAsync(Guid originalBillId, List<ReturnLineInput> returnLines, string? reason, string refundMethod, Guid? processorUserId)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var bill = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.PosBillId == originalBillId)
            ?? throw new InvalidOperationException("Original bill not found.");

        if (bill.Status != 2)
            throw new InvalidOperationException("Can only return items from completed bills.");
        if (returnLines is null || returnLines.Count == 0)
            throw new InvalidOperationException("Add at least one return line.");

        var posReturn = new PosReturn
        {
            PosReturnId = Guid.NewGuid(),
            ReturnNo = await GenerateReturnNoAsync(),
            OriginalBillId = originalBillId,
            StoreId = bill.StoreId,
            WarehouseId = bill.WarehouseId,
            CustomerId = bill.CustomerId,
            ReturnDate = DateTime.Today,
            Reason = reason,
            Status = 2, // processed immediately
            ProcessedAtUtc = DateTime.UtcNow,
            ProcessedByUserId = processorUserId,
            CompanyId = bill.CompanyId
        };

        decimal totalRefund = 0;

        foreach (var rl in returnLines)
        {
            if (rl.Qty <= 0)
                throw new InvalidOperationException("Return quantity must be greater than zero.");

            var originalLine = bill.Lines.FirstOrDefault(l => l.PosBillLineId == rl.OriginalBillLineId)
                ?? throw new InvalidOperationException("Original bill line not found.");

            var alreadyReturned = await _db.PosReturnLines
                .Where(l => l.OriginalBillLineId == rl.OriginalBillLineId)
                .SumAsync(l => l.Qty);
            var remainingQty = originalLine.Qty - alreadyReturned;
            if (rl.Qty > remainingQty)
                throw new InvalidOperationException($"Return qty ({rl.Qty}) exceeds remaining qty ({remainingQty}) for {originalLine.ItemNameSnapshot}.");

            var refundAmount = rl.Qty * originalLine.UnitPrice;
            totalRefund += refundAmount;

            posReturn.Lines.Add(new PosReturnLine
            {
                PosReturnLineId = Guid.NewGuid(),
                PosReturnId = posReturn.PosReturnId,
                OriginalBillLineId = rl.OriginalBillLineId,
                ItemId = originalLine.ItemId,
                Qty = rl.Qty,
                UnitPrice = originalLine.UnitPrice,
                RefundAmount = refundAmount
            });

            // Reverse stock: add qty back
            var stock = await _db.Stocks.FirstOrDefaultAsync(s =>
                s.ItemId == originalLine.ItemId && s.WarehouseId == bill.WarehouseId);

            if (stock is not null)
            {
                stock.Quantity += rl.Qty;
            }
            else
            {
                _db.Stocks.Add(new Stock
                {
                    StockId = Guid.NewGuid(),
                    ItemId = originalLine.ItemId,
                    WarehouseId = bill.WarehouseId,
                    Quantity = rl.Qty,
                    CompanyId = bill.CompanyId
                });
            }

            // RETURN stock transaction
            _db.StockTransactions.Add(new StockTransaction
            {
                StockTransactionId = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                Type = "RETURN",
                ItemId = originalLine.ItemId,
                WarehouseId = bill.WarehouseId,
                StoreId = bill.StoreId,
                Qty = rl.Qty, // positive = stock coming back
                RefType = "PosReturn",
                RefId = posReturn.PosReturnId.ToString(),
                Reason = $"Return from bill {bill.BillNo}" + (string.IsNullOrEmpty(reason) ? "" : $": {reason}"),
                UnitPrice = originalLine.UnitPrice,
                CompanyId = bill.CompanyId
            });
        }

        posReturn.TotalRefund = totalRefund;
        _db.PosReturns.Add(posReturn);

        // Refund payment record
        _db.Payments.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            PosBillId = originalBillId,
            PosReturnId = posReturn.PosReturnId,
            Method = refundMethod,
            Amount = totalRefund,
            IsRefund = true,
            PaidAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        try
        {
            await _audit.LogAsync(
                action: "PosReturnProcessed",
                entityType: "PosReturn",
                entityId: posReturn.PosReturnId.ToString(),
                data: new
                {
                    posReturn.ReturnNo,
                    OriginalBillNo = bill.BillNo,
                    posReturn.TotalRefund,
                    LineCount = posReturn.Lines.Count
                });
        }
        catch { }

        return posReturn.PosReturnId;
    }

    // ────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────

    private void RecalcTotals(PosBill bill)
    {
        bill.SubTotal = bill.Lines.Sum(l => l.Qty * l.UnitPrice);
        
        // Line-level discounts
        var lineDiscounts = bill.Lines.Sum(l => l.DiscountAmount);
        
        // Bill-level additional discount
        if (bill.AddDiscountPercent > 0)
            bill.AddDiscountAmount = Math.Round(bill.SubTotal * bill.AddDiscountPercent / 100m, 2);
        else
            bill.AddDiscountAmount = 0;

        bill.DiscountTotal = lineDiscounts + bill.CouponDiscount + bill.LoyaltyDiscount + bill.AddDiscountAmount;

        // Bill-level additional charge
        if (bill.AddChargePercent > 0)
            bill.AddChargeAmount = Math.Round(bill.SubTotal * bill.AddChargePercent / 100m, 2);
        else
            bill.AddChargeAmount = 0;

        decimal tax = 0;
        foreach (var l in bill.Lines)
        {
            if (l.GstPercentSnapshot is > 0)
            {
                var taxableAmount = l.Qty * l.UnitPrice - l.DiscountAmount;
                tax += taxableAmount * l.GstPercentSnapshot.Value / 100m;
            }
        }
        bill.TaxTotal = Math.Round(tax, 2);

        var preRound = bill.SubTotal - bill.DiscountTotal + bill.TaxTotal + bill.AddChargeAmount;
        bill.RoundOff = Math.Round(preRound) - preRound;
        bill.GrandTotal = preRound + bill.RoundOff;
        if (bill.GrandTotal < 0) bill.GrandTotal = 0;
    }

    private async Task<string> GenerateBillNoAsync()
    {
        var today = DateTime.Today;
        var prefix = $"POS-{today:yyyyMMdd}-";

        var last = await _db.PosBills
            .IgnoreQueryFilters()
            .Where(b => b.BillNo.StartsWith(prefix))
            .OrderByDescending(b => b.BillNo)
            .Select(b => b.BillNo)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var numPart = last.Replace(prefix, "");
            if (int.TryParse(numPart, out var n)) next = n + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private async Task<string> GenerateReturnNoAsync()
    {
        var today = DateTime.Today;
        var prefix = $"RET-{today:yyyyMMdd}-";

        var last = await _db.PosReturns
            .Where(r => r.ReturnNo.StartsWith(prefix))
            .OrderByDescending(r => r.ReturnNo)
            .Select(r => r.ReturnNo)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var numPart = last.Replace(prefix, "");
            if (int.TryParse(numPart, out var n)) next = n + 1;
        }

        return $"{prefix}{next:0000}";
    }

    // ── DTOs ──

    public class ItemLookupResult
    {
        public Guid ItemId { get; set; }
        public string SKU { get; set; } = "";
        public string? Barcode { get; set; }
        public string Name { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal? MRP { get; set; }
        public decimal? GstPercent { get; set; }
        public string? HsnCode { get; set; }
        public string? UnitName { get; set; }
        public string? CategoryName { get; set; }
        public decimal StockAvailable { get; set; }
    }

    public class ReturnLineInput
    {
        public Guid OriginalBillLineId { get; set; }
        public decimal Qty { get; set; }
    }

    public class HeldBillInfo
    {
        public Guid PosBillId { get; set; }
        public string BillNo { get; set; } = "";
        public DateTime BillDate { get; set; }
        public string StoreName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public int ItemCount { get; set; }
        public decimal GrandTotal { get; set; }
    }
}

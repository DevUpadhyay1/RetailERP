using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Phase 7: End-of-Day report generation and cash reconciliation.
/// </summary>
public class EodService
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public EodService(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>Generate (or refresh) an EOD report for a store + date.</summary>
    public async Task<EodReport> GenerateReportAsync(Guid storeId, DateTime reportDate, decimal openingCash)
    {
        var date = reportDate.Date;

        // Check if already exists
        var existing = await _db.EodReports
            .FirstOrDefaultAsync(r => r.StoreId == storeId && r.ReportDate == date);

        if (existing is not null && existing.Status == 2)
            throw new InvalidOperationException("This day is already closed.");

        // Aggregate completed bills for the day
        var bills = await _db.PosBills
            .AsNoTracking()
            .Include(b => b.Payments)
            .Where(b => b.StoreId == storeId && b.BillDate == date && b.Status == 2)
            .ToListAsync();

        var returns = await _db.PosReturns
            .AsNoTracking()
            .Where(r => r.StoreId == storeId && r.ReturnDate == date && r.Status == 2)
            .ToListAsync();

        var allPayments = bills.SelectMany(b => b.Payments.Where(p => !p.IsRefund)).ToList();

        var totalCash = allPayments.Where(p => p.Method == "Cash").Sum(p => p.Amount);
        var totalCard = allPayments.Where(p => p.Method == "Card").Sum(p => p.Amount);
        var totalUpi = allPayments.Where(p => p.Method == "UPI").Sum(p => p.Amount);
        var totalSales = bills.Sum(b => b.GrandTotal);
        var totalReturns = returns.Sum(r => r.TotalRefund);
        var netSales = totalSales - totalReturns;
        var expectedCash = openingCash + totalCash - returns.Sum(r => r.TotalRefund); // simplified: all returns refunded as cash

        if (existing is not null)
        {
            // Refresh the report
            existing.OpeningCash = openingCash;
            existing.TotalCashSales = totalCash;
            existing.TotalCardSales = totalCard;
            existing.TotalUpiSales = totalUpi;
            existing.TotalSales = totalSales;
            existing.TotalReturns = totalReturns;
            existing.NetSales = netSales;
            existing.ExpectedCash = expectedCash;
            existing.BillCount = bills.Count;
            existing.ReturnCount = returns.Count;
            await _db.SaveChangesAsync();
            return existing;
        }

        var report = new EodReport
        {
            EodReportId = Guid.NewGuid(),
            StoreId = storeId,
            ReportDate = date,
            OpeningCash = openingCash,
            TotalCashSales = totalCash,
            TotalCardSales = totalCard,
            TotalUpiSales = totalUpi,
            TotalSales = totalSales,
            TotalReturns = totalReturns,
            NetSales = netSales,
            ExpectedCash = expectedCash,
            ActualCash = 0,
            Variance = 0,
            BillCount = bills.Count,
            ReturnCount = returns.Count,
            Status = 1
        };

        _db.EodReports.Add(report);
        await _db.SaveChangesAsync();
        return report;
    }

    /// <summary>Close the day: record actual cash, calculate variance, mark as Closed.</summary>
    public async Task CloseAsync(Guid eodReportId, decimal actualCash, string? notes, Guid? closedByUserId)
    {
        var report = await _db.EodReports.FirstOrDefaultAsync(r => r.EodReportId == eodReportId)
            ?? throw new InvalidOperationException("Report not found.");

        if (report.Status == 2) throw new InvalidOperationException("Already closed.");

        report.ActualCash = actualCash;
        report.Variance = actualCash - report.ExpectedCash;
        report.Notes = notes;
        report.Status = 2;
        report.ClosedByUserId = closedByUserId;
        report.ClosedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        try
        {
            await _audit.LogAsync(
                action: "EodClosed",
                entityType: "EodReport",
                entityId: report.EodReportId.ToString(),
                data: new
                {
                    report.StoreId,
                    report.ReportDate,
                    report.NetSales,
                    report.Variance,
                    report.BillCount
                });
        }
        catch { }
    }
}

using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>Sprint 15 – Franchise agreement management and royalty calculation.</summary>
public sealed class FranchiseService
{
    private readonly ApplicationDbContext _db;

    public FranchiseService(ApplicationDbContext db)
    {
        _db = db;
    }

    // ── Agreement CRUD helpers ──

    public async Task<List<FranchiseAgreement>> GetAgreementsAsync(
        string? q = null, byte? status = null, int page = 1, int pageSize = 25)
    {
        var query = _db.FranchiseAgreements
            .AsNoTracking()
            .Include(a => a.FranchisorCompany)
            .Include(a => a.FranchiseeCompany)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a =>
                a.AgreementCode.Contains(q) ||
                a.FranchisorCompany!.Name.Contains(q) ||
                a.FranchiseeCompany!.Name.Contains(q) ||
                a.Territory.Contains(q));

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountAgreementsAsync(string? q = null, byte? status = null)
    {
        var query = _db.FranchiseAgreements.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a =>
                a.AgreementCode.Contains(q) ||
                a.FranchisorCompany!.Name.Contains(q) ||
                a.FranchiseeCompany!.Name.Contains(q) ||
                a.Territory.Contains(q));

        return await query.CountAsync();
    }

    public async Task<FranchiseAgreement?> GetByIdAsync(Guid id)
    {
        return await _db.FranchiseAgreements
            .Include(a => a.FranchisorCompany)
            .Include(a => a.FranchiseeCompany)
            .Include(a => a.RoyaltyPayments.OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth))
            .FirstOrDefaultAsync(a => a.FranchiseAgreementId == id);
    }

    public async Task<(bool Ok, string? Error)> CreateAgreementAsync(FranchiseAgreement agreement)
    {
        if (agreement.FranchisorCompanyId == agreement.FranchiseeCompanyId)
            return (false, "Franchisor and franchisee cannot be the same company.");

        var exists = await _db.FranchiseAgreements.AnyAsync(a =>
            a.FranchisorCompanyId == agreement.FranchisorCompanyId &&
            a.FranchiseeCompanyId == agreement.FranchiseeCompanyId);
        if (exists)
            return (false, "An agreement already exists between these companies.");

        var codeExists = await _db.FranchiseAgreements.AnyAsync(a => a.AgreementCode == agreement.AgreementCode);
        if (codeExists)
            return (false, "Agreement code is already in use.");

        _db.FranchiseAgreements.Add(agreement);

        var franchisee = await _db.Companies.FindAsync(agreement.FranchiseeCompanyId);
        if (franchisee != null)
        {
            franchisee.ParentCompanyId = agreement.FranchisorCompanyId;
            franchisee.BusinessType = BusinessType.Franchise;
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task UpdateAgreementAsync(FranchiseAgreement agreement)
    {
        _db.FranchiseAgreements.Update(agreement);
        await _db.SaveChangesAsync();
    }

    // ── Royalty Calculation ──

    /// <summary>
    /// Calculate royalty for a given agreement and period.
    /// Pulls gross sales from POS bills of the franchisee company during the period.
    /// </summary>
    public async Task<RoyaltyCalculation> CalculateRoyaltyAsync(Guid agreementId, int year, int month)
    {
        var agreement = await _db.FranchiseAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FranchiseAgreementId == agreementId);

        if (agreement is null)
            return new RoyaltyCalculation { Error = "Agreement not found." };

        var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var grossSales = await _db.PosBills
            .IgnoreQueryFilters()
            .Where(b => b.CompanyId == agreement.FranchiseeCompanyId
                     && b.Status == 2 // Completed
                     && b.BillDate >= periodStart
                     && b.BillDate < periodEnd)
            .SumAsync(b => b.GrandTotal);

        var royaltyAmount = grossSales * agreement.RoyaltyPercent / 100m;
        var totalDue = Math.Max(royaltyAmount + agreement.MonthlyFlatFee, agreement.MinMonthlyRoyalty);

        return new RoyaltyCalculation
        {
            GrossSales = grossSales,
            RoyaltyPercent = agreement.RoyaltyPercent,
            RoyaltyAmount = Math.Round(royaltyAmount, 2),
            FlatFeeAmount = agreement.MonthlyFlatFee,
            MinMonthlyRoyalty = agreement.MinMonthlyRoyalty,
            TotalDue = Math.Round(totalDue, 2)
        };
    }

    /// <summary>Generate or update a royalty payment record for a period.</summary>
    public async Task<(bool Ok, string? Error, RoyaltyPayment? Payment)> GenerateRoyaltyPaymentAsync(
        Guid agreementId, int year, int month)
    {
        var existing = await _db.RoyaltyPayments
            .FirstOrDefaultAsync(r => r.FranchiseAgreementId == agreementId
                                   && r.PeriodYear == year
                                   && r.PeriodMonth == (byte)month);

        if (existing is not null && existing.Status == 2)
            return (false, "Payment already recorded for this period.", null);

        var calc = await CalculateRoyaltyAsync(agreementId, year, month);
        if (calc.Error is not null)
            return (false, calc.Error, null);

        if (existing is not null)
        {
            existing.GrossSales = calc.GrossSales;
            existing.RoyaltyAmount = calc.RoyaltyAmount;
            existing.FlatFeeAmount = calc.FlatFeeAmount;
            existing.TotalDue = calc.TotalDue;
            existing.Status = 1;
            await _db.SaveChangesAsync();
            return (true, null, existing);
        }

        var payment = new RoyaltyPayment
        {
            FranchiseAgreementId = agreementId,
            PeriodYear = year,
            PeriodMonth = (byte)month,
            GrossSales = calc.GrossSales,
            RoyaltyAmount = calc.RoyaltyAmount,
            FlatFeeAmount = calc.FlatFeeAmount,
            TotalDue = calc.TotalDue,
            Status = 1
        };

        _db.RoyaltyPayments.Add(payment);
        await _db.SaveChangesAsync();
        return (true, null, payment);
    }

    /// <summary>Mark a royalty payment as paid.</summary>
    public async Task<(bool Ok, string? Error)> RecordPaymentAsync(Guid paymentId, decimal amountPaid, string? remarks)
    {
        var payment = await _db.RoyaltyPayments.FindAsync(paymentId);
        if (payment is null) return (false, "Payment record not found.");

        payment.AmountPaid = amountPaid;
        payment.PaidAtUtc = DateTime.UtcNow;
        payment.Status = 2; // Paid
        payment.Remarks = remarks;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ── Dashboard aggregates ──

    public async Task<FranchiseDashboard> GetDashboardAsync()
    {
        var totalAgreements = await _db.FranchiseAgreements.CountAsync();
        var activeAgreements = await _db.FranchiseAgreements.CountAsync(a => a.Status == 1);
        var totalPending = await _db.RoyaltyPayments
            .Where(r => r.Status == 1)
            .SumAsync(r => (decimal?)r.TotalDue) ?? 0;
        var totalCollected = await _db.RoyaltyPayments
            .Where(r => r.Status == 2)
            .SumAsync(r => (decimal?)r.AmountPaid) ?? 0;

        var recentPayments = await _db.RoyaltyPayments
            .AsNoTracking()
            .Include(r => r.Agreement).ThenInclude(a => a!.FranchiseeCompany)
            .OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth)
            .Take(10)
            .ToListAsync();

        return new FranchiseDashboard
        {
            TotalAgreements = totalAgreements,
            ActiveAgreements = activeAgreements,
            TotalPendingRoyalty = totalPending,
            TotalCollectedRoyalty = totalCollected,
            RecentPayments = recentPayments
        };
    }
}

// ── Result types ──

public class RoyaltyCalculation
{
    public decimal GrossSales { get; set; }
    public decimal RoyaltyPercent { get; set; }
    public decimal RoyaltyAmount { get; set; }
    public decimal FlatFeeAmount { get; set; }
    public decimal MinMonthlyRoyalty { get; set; }
    public decimal TotalDue { get; set; }
    public string? Error { get; set; }
}

public class FranchiseDashboard
{
    public int TotalAgreements { get; set; }
    public int ActiveAgreements { get; set; }
    public decimal TotalPendingRoyalty { get; set; }
    public decimal TotalCollectedRoyalty { get; set; }
    public List<RoyaltyPayment> RecentPayments { get; set; } = new();
}

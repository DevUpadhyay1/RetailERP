using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 7: End-of-Day closing report per store.
/// Status: 1 = Pending (generated, not yet reconciled), 2 = Closed (manager verified).
/// </summary>
public class EodReport : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid EodReportId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReportDate { get; set; } = DateTime.Today;

    [Precision(18, 2)]
    public decimal OpeningCash { get; set; }

    [Precision(18, 2)]
    public decimal TotalCashSales { get; set; }

    [Precision(18, 2)]
    public decimal TotalCardSales { get; set; }

    [Precision(18, 2)]
    public decimal TotalUpiSales { get; set; }

    [Precision(18, 2)]
    public decimal TotalSales { get; set; }

    [Precision(18, 2)]
    public decimal TotalReturns { get; set; }

    [Precision(18, 2)]
    public decimal NetSales { get; set; }

    [Precision(18, 2)]
    public decimal ExpectedCash { get; set; }

    [Precision(18, 2)]
    public decimal ActualCash { get; set; }

    [Precision(18, 2)]
    public decimal Variance { get; set; }

    public int BillCount { get; set; }
    public int ReturnCount { get; set; }

    // 1 = Pending, 2 = Closed
    public byte Status { get; set; } = 1;

    public Guid? ClosedByUserId { get; set; }
    public ApplicationUser? ClosedByUser { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;
using RetailERP.Data.Identity;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 5: POS Return header. Links back to original PosBill.
/// Status: 1 = Pending, 2 = Processed. Creates RETURN StockTransactions + refund Payment.
/// </summary>
public class PosReturn : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid PosReturnId { get; set; } = Guid.NewGuid();

    [Required, StringLength(30)]
    public string ReturnNo { get; set; } = string.Empty;

    [Required]
    public Guid OriginalBillId { get; set; }
    public PosBill? OriginalBill { get; set; }

    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [DataType(DataType.Date)]
    public DateTime ReturnDate { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Reason { get; set; }

    [Precision(18, 2)]
    public decimal TotalRefund { get; set; }

    // 1 = Pending, 2 = Processed
    public byte Status { get; set; } = 1;
    public DateTime? ProcessedAtUtc { get; set; }

    // Who processed the return
    public Guid? ProcessedByUserId { get; set; }
    public ApplicationUser? ProcessedByUser { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<PosReturnLine> Lines { get; set; } = new();
}

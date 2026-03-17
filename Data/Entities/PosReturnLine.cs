using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

/// <summary>
/// Phase 5: Individual line on a POS return. References the original bill line.
/// </summary>
public class PosReturnLine : IAuditableEntity
{
    [Key]
    public Guid PosReturnLineId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PosReturnId { get; set; }
    public PosReturn? PosReturn { get; set; }

    // Original bill line for traceability
    public Guid? OriginalBillLineId { get; set; }
    public PosBillLine? OriginalBillLine { get; set; }

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Precision(18, 2)]
    [Range(0.0001, 999999999)]
    public decimal Qty { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }

    [Precision(18, 2)]
    public decimal RefundAmount { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

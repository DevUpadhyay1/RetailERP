using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class InvoiceLine : IAuditableEntity
{
    [Key]
    public Guid InvoiceLineId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Precision(18, 2)]
    [Range(0.0001, 999999999)]
    public decimal Qty { get; set; } = 1;

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; } = 0;

    // Snapshot fields (professional invoices should not change when master data changes)
    [StringLength(50)]
    public string? ItemSkuSnapshot { get; set; }

    [StringLength(200)]
    public string? ItemNameSnapshot { get; set; }

    [Precision(5, 2)]
    [Range(0, 100)]
    public decimal? GstPercentSnapshot { get; set; }

    [StringLength(20)]
    public string? HsnCodeSnapshot { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal DiscountAmount { get; set; } = 0;

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
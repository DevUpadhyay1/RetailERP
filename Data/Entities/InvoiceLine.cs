using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RetailERP.Data.Entities;

public class InvoiceLine
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
}
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RetailERP.Data.Entities;

public class Stock
{
    [Key]
    public Guid StockId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal Quantity { get; set; } = 0;
}
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RetailERP.Data.Entities;

public class Item
{
    [Key]
    public Guid ItemId { get; set; } = Guid.NewGuid();

    [Required, StringLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Precision(18, 2)]
    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; } = 0;

    [Range(0, 999999)]
    public int ReorderLevel { get; set; } = 0;

    public bool IsActive { get; set; } = true;

}
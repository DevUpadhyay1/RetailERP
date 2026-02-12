using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

public class Warehouse
{
    [Key]
    public Guid WarehouseId { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Address { get; set; }
}
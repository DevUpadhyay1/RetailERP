using System.ComponentModel.DataAnnotations;

namespace RetailERP.Models;

public class StockAdjustVm
{
    [Required]
    public Guid StockId { get; set; }

    public string ItemLabel { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public decimal CurrentQty { get; set; }

    [Required]
    public decimal DeltaQty { get; set; } // + add, - remove

    [MaxLength(200)]
    public string? Reason { get; set; }

    public string? ReturnUrl { get; set; }
}
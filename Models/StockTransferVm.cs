using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RetailERP.Models;

public class StockTransferVm
{
    [Required]
    public Guid FromWarehouseId { get; set; }

    [Required]
    public Guid ToWarehouseId { get; set; }

    [Required]
    public Guid ItemId { get; set; }

    [Range(1, 999999)]
    public int Qty { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    public List<SelectListItem> Warehouses { get; set; } = new();
    public List<SelectListItem> Items { get; set; } = new();
}
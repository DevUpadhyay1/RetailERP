using System.ComponentModel.DataAnnotations;

namespace RetailERP.Models.Api;

// ══════════════════════════════════════════
// Item DTOs
// ══════════════════════════════════════════

public sealed class ItemDto
{
    public Guid ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? MRP { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? GstPercent { get; set; }
    public string? HsnCode { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; }
    public Guid? UnitId { get; set; }
    public string? UnitName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

public class ItemCreateDto
{
    [Required, StringLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Barcode { get; set; }

    [Range(0, 999999999)]
    public decimal UnitPrice { get; set; }

    [Range(0, 999999999)]
    public decimal? MRP { get; set; }

    [Range(0, 999999999)]
    public decimal? PurchasePrice { get; set; }

    [Range(0, 100)]
    public decimal? GstPercent { get; set; }

    [StringLength(20)]
    public string? HsnCode { get; set; }

    [Range(0, 999999)]
    public int ReorderLevel { get; set; }

    public Guid? UnitId { get; set; }
    public Guid? CategoryId { get; set; }
}

public sealed class ItemUpdateDto : ItemCreateDto
{
    public bool IsActive { get; set; } = true;
}

// ══════════════════════════════════════════
// Category DTOs
// ══════════════════════════════════════════

public sealed class CategoryDto
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CategoryCreateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
}

// ══════════════════════════════════════════
// Unit DTOs
// ══════════════════════════════════════════

public sealed class UnitDto
{
    public Guid UnitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UnitCreateDto
{
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Symbol { get; set; }
}

// ══════════════════════════════════════════
// Store DTOs
// ══════════════════════════════════════════

public sealed class StoreDto
{
    public Guid StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? GstNo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class StoreCreateDto
{
    [Required, StringLength(50)]
    public string StoreCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(15, MinimumLength = 15, ErrorMessage = "GSTIN must be exactly 15 characters")]
    [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Enter a valid GSTIN")]
    public string? GstNo { get; set; }
}

// ══════════════════════════════════════════
// Customer DTOs
// ══════════════════════════════════════════

public sealed class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed class CustomerCreateDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(200), EmailAddress]
    public string? Email { get; set; }
}

// ══════════════════════════════════════════
// Supplier DTOs
// ══════════════════════════════════════════

public sealed class SupplierDto
{
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SupplierCreateDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(15)]
    [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit Indian mobile number")]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(200), EmailAddress]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

// ══════════════════════════════════════════
// Warehouse DTOs
// ══════════════════════════════════════════

public sealed class WarehouseDto
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
}

public sealed class WarehouseCreateDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Address { get; set; }

    public Guid? StoreId { get; set; }
}

// ══════════════════════════════════════════
// Stock DTOs
// ══════════════════════════════════════════

public sealed class StockDto
{
    public Guid StockId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public sealed class StockAdjustDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    public decimal AdjustmentQty { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }
}

// ══════════════════════════════════════════
// POS DTOs
// ══════════════════════════════════════════

public sealed class PosBillDto
{
    public Guid PosBillId { get; set; }
    public string BillNo { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public Guid StoreId { get; set; }
    public string? StoreName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public byte Status { get; set; }
    public string StatusName => Status switch { 1 => "Open", 2 => "Completed", 3 => "Cancelled", _ => "Unknown" };
    public List<PosBillLineDto> Lines { get; set; } = new();
}

public sealed class PosBillLineDto
{
    public Guid PosBillLineId { get; set; }
    public Guid ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? SKU { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

// ══════════════════════════════════════════
// Report DTOs
// ══════════════════════════════════════════

public sealed class SalesReportQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class SalesReportDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalBills { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscount { get; set; }
    public List<DailySalesDto> Daily { get; set; } = new();
}

public sealed class DailySalesDto
{
    public DateTime Date { get; set; }
    public int BillCount { get; set; }
    public decimal Revenue { get; set; }
}

// ══════════════════════════════════════════
// Invoice DTOs
// ══════════════════════════════════════════

public sealed class InvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? EmployeeName { get; set; }
    public decimal TotalAmount { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? PostedAt { get; set; }
    public List<InvoiceLineDto> Lines { get; set; } = new();
}

public sealed class InvoiceLineDto
{
    public Guid InvoiceLineId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

// ══════════════════════════════════════════
// Purchase DTOs
// ══════════════════════════════════════════

public sealed class PurchaseDto
{
    public Guid PurchaseId { get; set; }
    public string PurchaseNo { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? EmployeeName { get; set; }
    public decimal TotalAmount { get; set; }
    public byte Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime? ReceivedAt { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineDto> Lines { get; set; } = new();
}

public sealed class PurchaseLineDto
{
    public Guid PurchaseLineId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

// ══════════════════════════════════════════
// Promotion DTOs
// ══════════════════════════════════════════

public sealed class PromotionDto
{
    public Guid PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PromoType { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int BuyQty { get; set; }
    public int GetQty { get; set; }
    public Guid? FreeItemId { get; set; }
    public string? FreeItemName { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public TimeSpan? HappyHourStart { get; set; }
    public TimeSpan? HappyHourEnd { get; set; }
    public decimal MinBillAmount { get; set; }
    public int MaxUsesTotal { get; set; }
    public int UsedCount { get; set; }
    public int Priority { get; set; }
    public bool IsExclusive { get; set; }
    public bool IsActive { get; set; }
}

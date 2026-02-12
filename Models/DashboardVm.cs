namespace RetailERP.Models;

public sealed class DashboardVm
{
    public int ItemsCount { get; set; }
    public int CustomersCount { get; set; }
    public int EmployeesCount { get; set; }
    public int WarehousesCount { get; set; }

    public int DraftInvoicesCount { get; set; }
    public int PostedLast7DaysCount { get; set; }
    public decimal SalesLast7Days { get; set; }

    public List<RecentInvoiceRow> RecentInvoices { get; set; } = new();
    public List<LowStockRow> LowStock { get; set; } = new();
}

public sealed class RecentInvoiceRow
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public byte Status { get; set; }
}

public sealed class LowStockRow
{
    public string ItemSku { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string WarehouseName { get; set; } = "";
    public decimal Quantity { get; set; }
    public int ReorderLevel { get; set; }
}
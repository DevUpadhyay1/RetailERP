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

    public decimal SalesThisMonth { get; set; }
    public decimal PurchasesThisMonth { get; set; }
    public int LowStockCount { get; set; }

    // POS Billing KPIs
    public decimal PosSalesThisMonth { get; set; }
    public decimal PosSalesLast7Days { get; set; }
    public int OpenPosBillsCount { get; set; }
    public int CompletedPosBillsLast7Days { get; set; }

    // Combined totals (Invoice + POS)
    public decimal TotalSalesThisMonth => SalesThisMonth + PosSalesThisMonth;
    public decimal TotalSalesLast7Days => SalesLast7Days + PosSalesLast7Days;

    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> SalesDaily { get; set; } = new();
    public List<decimal> PosSalesDaily { get; set; } = new();
    public List<decimal> PurchasesDaily { get; set; } = new();

    public List<RecentInvoiceRow> RecentInvoices { get; set; } = new();
    public List<RecentPosBillRow> RecentPosBills { get; set; } = new();
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

public sealed class RecentPosBillRow
{
    public Guid PosBillId { get; set; }
    public string BillNo { get; set; } = "";
    public DateTime BillDate { get; set; }
    public string CustomerName { get; set; } = "-";
    public string StoreName { get; set; } = "-";
    public decimal GrandTotal { get; set; }
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
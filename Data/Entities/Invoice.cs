using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data.Auditing;

namespace RetailERP.Data.Entities;

public class Invoice : IAuditableEntity, ITenantEntity
{
    [Key]
    public Guid InvoiceId { get; set; } = Guid.NewGuid();

    [Required, StringLength(30)]
    public string InvoiceNo { get; set; } = string.Empty;

    [Required]
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [DataType(DataType.Date)]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; } = 0;

    public byte Status { get; set; } = 1; // 1=Draft, 2=Posted
    public DateTime? PostedAt { get; set; }

    // Optional multi-company support
    public Guid? CompanyId { get; set; }

    // Auditing
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();
}
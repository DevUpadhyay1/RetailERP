using System.ComponentModel.DataAnnotations;

namespace RetailERP.Data.Entities;

/// <summary>Sprint 6 – Company-customisable receipt / invoice template layout.</summary>
public class BillTemplate : ITenantEntity
{
    [Key]
    public Guid BillTemplateId { get; set; } = Guid.NewGuid();

    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    [Required, StringLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>1 = POS Receipt, 2 = Invoice</summary>
    public byte TemplateType { get; set; } = 1;

    /// <summary>Document type this template is designed for (mainly used when TemplateType = Invoice).</summary>
    public InvoiceDocumentType DocumentType { get; set; } = InvoiceDocumentType.TaxInvoice;

    /// <summary>Company default or store-specific override.</summary>
    public InvoiceTemplateScope TemplateScope { get; set; } = InvoiceTemplateScope.Company;

    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    /// <summary>Thermal58mm, Thermal80mm, A4, A5</summary>
    [Required, StringLength(20)]
    public string PaperSize { get; set; } = "Thermal80mm";

    /// <summary>
    /// Serialised JSON array of draggable components:
    /// [{ "id":"store_header", "x":0, "y":0, "w":12, "h":2, "visible":true, "props":{} }, …]
    /// </summary>
    [Required]
    public string LayoutJson { get; set; } = "[]";

    [StringLength(500)]
    public string? HeaderText { get; set; }

    [StringLength(500)]
    public string? FooterText { get; set; }

    public bool ShowLogo { get; set; } = true;
    public bool ShowGst { get; set; } = true;
    public bool ShowBarcode { get; set; }
    public bool ShowSignature { get; set; } = true;
    public bool ShowStamp { get; set; }
    public bool ShowPartyBalance { get; set; }
    public bool EnableFreeItemQuantity { get; set; }
    public bool ShowItemDescription { get; set; } = true;
    public bool ShowPhoneOnInvoice { get; set; } = true;

    public int FontSize { get; set; } = 12;

    [StringLength(40)]
    public string ThemeName { get; set; } = "modern";

    [StringLength(20)]
    public string AccentColor { get; set; } = "#0d6efd";

    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

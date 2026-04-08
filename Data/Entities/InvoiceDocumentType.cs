namespace RetailERP.Data.Entities;

/// <summary>
/// Professional invoice document categorization for tenant-customizable invoicing.
/// </summary>
public enum InvoiceDocumentType : byte
{
    TaxInvoice = 1,
    BillOfSupply = 2,
    CreditNote = 3,
    DebitNote = 4,
    ProformaInvoice = 5
}

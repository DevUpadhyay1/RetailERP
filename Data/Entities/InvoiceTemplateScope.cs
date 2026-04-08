namespace RetailERP.Data.Entities;

/// <summary>
/// Determines where a template applies.
/// Company: fallback default for all stores in the tenant.
/// Store: store-specific override.
/// </summary>
public enum InvoiceTemplateScope : byte
{
    Company = 1,
    Store = 2
}

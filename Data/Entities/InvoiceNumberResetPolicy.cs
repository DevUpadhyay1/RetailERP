namespace RetailERP.Data.Entities;

/// <summary>
/// Rule for when invoice sequence should reset.
/// </summary>
public enum InvoiceNumberResetPolicy : byte
{
    Never = 1,
    Yearly = 2,
    Monthly = 3
}

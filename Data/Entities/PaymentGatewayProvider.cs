namespace RetailERP.Data.Entities;

/// <summary>
/// Sprint 16 – Defines the payment gateway provider used by a specific tenant.
/// </summary>
public enum PaymentGatewayProvider : byte
{
    None = 0,
    Razorpay = 1,
    Stripe = 2
}

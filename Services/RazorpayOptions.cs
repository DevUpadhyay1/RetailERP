namespace RetailERP.Services;

/// <summary>
/// Sprint 2: Razorpay configuration — bound from appsettings + User Secrets.
/// </summary>
public class RazorpayOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
}

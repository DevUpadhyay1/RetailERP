using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace RetailERP.Services;

/// <summary>
/// Sprint 2: Razorpay REST API client.
/// Creates orders, verifies payment signatures, processes refunds.
/// Uses HttpClient instead of legacy NuGet SDK for full .NET 8 compatibility.
/// </summary>
public class RazorpayService
{
    private readonly HttpClient _http;
    private readonly RazorpayOptions _opts;
    private readonly ILogger<RazorpayService> _log;
    private readonly IServiceProvider _serviceProvider;
    private const string BaseUrl = "https://api.razorpay.com/v1";

    public RazorpayService(HttpClient http, IOptions<RazorpayOptions> opts, ILogger<RazorpayService> log, IServiceProvider serviceProvider)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
        _serviceProvider = serviceProvider;

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private async Task<(string KeyId, string KeySecret)> GetCredentialsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var tenant = scope.ServiceProvider.GetService<ITenantProvider>();
        if (tenant != null)
        {
            var tenantId = tenant.CompanyId;
            if (tenantId.HasValue)
            {
                var db = scope.ServiceProvider.GetRequiredService<RetailERP.Data.ApplicationDbContext>();
                var company = await db.Companies.FindAsync(tenantId.Value);
                if (company != null && company.GatewayProvider == RetailERP.Data.Entities.PaymentGatewayProvider.Razorpay
                    && !string.IsNullOrWhiteSpace(company.GatewayKeyId) && !string.IsNullOrWhiteSpace(company.GatewayKeySecret))
                {
                    return (company.GatewayKeyId, company.GatewayKeySecret);
                }
            }
        }
        return (_opts.KeyId, _opts.KeySecret);
    }

    private async Task SetAuthHeaderAsync()
    {
        var creds = await GetCredentialsAsync();
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{creds.KeyId}:{creds.KeySecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    // ────────────────────────────────────────────────────────
    // Create Order (server-side — required before accepting payment)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Create a Razorpay Order for a given amount.
    /// Amount is in RUPEES — Razorpay API expects PAISE, so we multiply by 100.
    /// </summary>
    public async Task<RazorpayOrder?> CreateOrderAsync(decimal amountInRupees, string currency = "INR", string? receiptId = null, string? notes = null)
    {
        var amountInPaise = (long)(amountInRupees * 100);

        var payload = new Dictionary<string, object>
        {
            ["amount"] = amountInPaise,
            ["currency"] = currency,
            ["receipt"] = receiptId ?? Guid.NewGuid().ToString("N")[..20],
            ["payment_capture"] = 1  // Auto-capture
        };

        if (!string.IsNullOrWhiteSpace(notes))
            payload["notes"] = new Dictionary<string, string> { ["info"] = notes };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _log.LogInformation("Razorpay: Creating order for ₹{Amount} ({Paise} paise)", amountInRupees, amountInPaise);

        await SetAuthHeaderAsync();
        var response = await _http.PostAsync($"{BaseUrl}/orders", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("Razorpay: Order creation failed. Status={Status}, Body={Body}", response.StatusCode, responseBody);
            return null;
        }

        var order = JsonSerializer.Deserialize<RazorpayOrder>(responseBody);
        _log.LogInformation("Razorpay: Order created. OrderId={OrderId}", order?.Id);
        return order;
    }

    // ────────────────────────────────────────────────────────
    // Verify Payment Signature (server-side validation)
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Verify the payment signature returned by Razorpay Checkout.
    /// This MUST be called server-side to confirm payment is genuine.
    /// Signature = HMAC-SHA256(order_id + "|" + payment_id, key_secret)
    /// </summary>
    public async Task<bool> VerifyPaymentSignatureAsync(string orderId, string paymentId, string signature)
    {
        var creds = await GetCredentialsAsync();
        var payload = $"{orderId}|{paymentId}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(creds.KeySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

        var isValid = string.Equals(expectedSignature, signature, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
            _log.LogWarning("Razorpay: Signature verification FAILED. OrderId={OrderId}, PaymentId={PaymentId}", orderId, paymentId);
        else
            _log.LogInformation("Razorpay: Signature verified. OrderId={OrderId}, PaymentId={PaymentId}", orderId, paymentId);

        return isValid;
    }

    // ────────────────────────────────────────────────────────
    // Fetch Payment Details
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Fetch payment details from Razorpay to get method, VPA, card info, etc.
    /// </summary>
    public async Task<RazorpayPaymentDetails?> FetchPaymentAsync(string paymentId)
    {
        await SetAuthHeaderAsync();
        var response = await _http.GetAsync($"{BaseUrl}/payments/{paymentId}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("Razorpay: Fetch payment failed. PaymentId={PaymentId}, Body={Body}", paymentId, body);
            return null;
        }

        return JsonSerializer.Deserialize<RazorpayPaymentDetails>(body);
    }

    // ────────────────────────────────────────────────────────
    // Process Refund
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Refund a payment (full or partial). Amount in RUPEES.
    /// </summary>
    public async Task<RazorpayRefund?> RefundAsync(string paymentId, decimal amountInRupees, string? notes = null)
    {
        var amountInPaise = (long)(amountInRupees * 100);

        var payload = new Dictionary<string, object> { ["amount"] = amountInPaise };
        if (!string.IsNullOrWhiteSpace(notes))
            payload["notes"] = new Dictionary<string, string> { ["reason"] = notes };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _log.LogInformation("Razorpay: Initiating refund for ₹{Amount} on PaymentId={PaymentId}", amountInRupees, paymentId);

        await SetAuthHeaderAsync();
        var response = await _http.PostAsync($"{BaseUrl}/payments/{paymentId}/refund", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("Razorpay: Refund failed. PaymentId={PaymentId}, Body={Body}", paymentId, responseBody);
            return null;
        }

        var refund = JsonSerializer.Deserialize<RazorpayRefund>(responseBody);
        _log.LogInformation("Razorpay: Refund created. RefundId={RefundId}", refund?.Id);
        return refund;
    }

    /// <summary>Returns the Razorpay Key ID (public key safe to send to browser).</summary>
    public async Task<string> GetPublicKeyAsync()
    {
        var creds = await GetCredentialsAsync();
        return creds.KeyId;
    }

    /// <summary>
    /// Validate Razorpay credentials without saving.
    /// 404 means auth passed for a non-existent test resource; 401/403 means invalid credentials.
    /// </summary>
    public async Task<(bool Success, string Message)> TestCredentialsAsync(string keyId, string keySecret)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
            return (false, "Key ID and Key Secret are required.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/payments/pay_test_connection");
            var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{keyId}:{keySecret}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

            var response = await _http.SendAsync(req);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return (false, "Invalid Razorpay credentials.");

            if (response.StatusCode == HttpStatusCode.NotFound || response.IsSuccessStatusCode)
                return (true, "Connection successful. Credentials look valid.");

            return (false, $"Razorpay returned {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Razorpay credential test failed.");
            return (false, "Unable to connect to Razorpay right now. Please try again.");
        }
    }
}

// ────────────────────────────────────────────────────────
// DTOs for Razorpay API responses
// ────────────────────────────────────────────────────────

public class RazorpayOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("entity")]
    public string Entity { get; set; } = "order";

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("amount_paid")]
    public long AmountPaid { get; set; }

    [JsonPropertyName("amount_due")]
    public long AmountDue { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "INR";

    [JsonPropertyName("receipt")]
    public string? Receipt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public class RazorpayPaymentDetails
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "INR";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;  // upi / card / netbanking / wallet

    [JsonPropertyName("vpa")]
    public string? Vpa { get; set; }  // UPI VPA (e.g., user@upi)

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("card_id")]
    public string? CardId { get; set; }

    [JsonPropertyName("bank")]
    public string? Bank { get; set; }

    [JsonPropertyName("wallet")]
    public string? Wallet { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }
}

public class RazorpayRefund
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "INR";

    [JsonPropertyName("payment_id")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;  // processed / pending
}

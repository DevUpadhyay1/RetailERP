using System.Net.Http.Headers;
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
    private const string BaseUrl = "https://api.razorpay.com/v1";

    public RazorpayService(HttpClient http, IOptions<RazorpayOptions> opts, ILogger<RazorpayService> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;

        // Basic Auth: key_id:key_secret
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_opts.KeyId}:{_opts.KeySecret}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
    public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
    {
        var payload = $"{orderId}|{paymentId}";
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(_opts.KeySecret));
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
    public string GetPublicKey() => _opts.KeyId;
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

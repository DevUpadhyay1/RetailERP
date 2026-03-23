using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RetailERP.Services;

public sealed class WhatsAppOptions
{
    public string PhoneNumberId { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Sprint 11: WhatsApp messaging via Meta Cloud API.
/// Supports text messages and receipt templates.
/// Falls back to logging when not configured.
/// </summary>
public sealed class WhatsAppService
{
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opts;
    private readonly ILogger<WhatsAppService> _log;

    public WhatsAppService(HttpClient http, IOptions<WhatsAppOptions> opts, ILogger<WhatsAppService> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
    }

    public bool IsConfigured => _opts.IsEnabled
        && !string.IsNullOrWhiteSpace(_opts.PhoneNumberId)
        && !string.IsNullOrWhiteSpace(_opts.AccessToken);

    public async Task<WhatsAppResult> SendTextAsync(string toPhone, string message)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("[WhatsApp] Not configured. Would send to {Phone}", MaskPhone(toPhone));
            return new WhatsAppResult { Success = true, MessageId = "SIMULATED", Simulated = true };
        }

        try
        {
            var to = toPhone.Replace("+", "").Replace(" ", "");
            if (!to.StartsWith("91") && to.Length == 10) to = "91" + to;

            var url = $"https://graph.facebook.com/v18.0/{_opts.PhoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "text",
                text = new { body = message }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_opts.AccessToken}");

            var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _log.LogInformation("[WhatsApp] Sent to {Phone}", MaskPhone(toPhone));
                return new WhatsAppResult { Success = true, MessageId = body };
            }

            _log.LogError("[WhatsApp] Failed to {Phone}: {Status}", MaskPhone(toPhone), resp.StatusCode);
            return new WhatsAppResult { Success = false, Error = body };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[WhatsApp] Exception sending to {Phone}", MaskPhone(toPhone));
            return new WhatsAppResult { Success = false, Error = ex.Message };
        }
    }

    private static string MaskPhone(string? phone)
    {
        var raw = (phone ?? string.Empty).Trim();
        if (raw.Length <= 4) return "****";
        return new string('*', raw.Length - 4) + raw[^4..];
    }

    public sealed class WhatsAppResult
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? Error { get; set; }
        public bool Simulated { get; set; }
    }
}

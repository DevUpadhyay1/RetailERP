using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace RetailERP.Services;

public sealed class TwilioOptions
{
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string FromNumber { get; set; } = "";
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Sprint 11: SMS sending via Twilio REST API.
/// Falls back to logging when Twilio is not configured.
/// </summary>
public sealed class SmsService
{
    private readonly HttpClient _http;
    private readonly TwilioOptions _opts;
    private readonly ILogger<SmsService> _log;

    public SmsService(HttpClient http, IOptions<TwilioOptions> opts, ILogger<SmsService> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
    }

    public bool IsConfigured => _opts.IsEnabled
        && !string.IsNullOrWhiteSpace(_opts.AccountSid)
        && !string.IsNullOrWhiteSpace(_opts.AuthToken)
        && !string.IsNullOrWhiteSpace(_opts.FromNumber);

    public async Task<SmsResult> SendAsync(string toPhone, string message)
    {
        if (!IsConfigured)
        {
            _log.LogWarning("[SMS] Twilio not configured. Would send to {Phone}", MaskPhone(toPhone));
            return new SmsResult { Success = true, MessageId = "SIMULATED", Simulated = true };
        }

        try
        {
            var to = toPhone.StartsWith("+") ? toPhone : $"+91{toPhone}";
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_opts.AccountSid}/Messages.json";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("To", to),
                new KeyValuePair<string, string>("From", _opts.FromNumber),
                new KeyValuePair<string, string>("Body", message)
            });

            var authBytes = Encoding.ASCII.GetBytes($"{_opts.AccountSid}:{_opts.AuthToken}");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            var resp = await _http.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _log.LogInformation("[SMS] Sent to {Phone}", MaskPhone(toPhone));
                return new SmsResult { Success = true, MessageId = body };
            }

            _log.LogError("[SMS] Failed to {Phone}: {Status}", MaskPhone(toPhone), resp.StatusCode);
            return new SmsResult { Success = false, Error = body };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[SMS] Exception sending to {Phone}", MaskPhone(toPhone));
            return new SmsResult { Success = false, Error = ex.Message };
        }
    }

    private static string MaskPhone(string? phone)
    {
        var raw = (phone ?? string.Empty).Trim();
        if (raw.Length <= 4) return "****";
        return new string('*', raw.Length - 4) + raw[^4..];
    }

    public sealed class SmsResult
    {
        public bool Success { get; set; }
        public string? MessageId { get; set; }
        public string? Error { get; set; }
        public bool Simulated { get; set; }
    }
}

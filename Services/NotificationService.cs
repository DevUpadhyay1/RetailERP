using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using RetailERP.Data;
using RetailERP.Data.Entities;

namespace RetailERP.Services;

/// <summary>
/// Sprint 11: Orchestrates sending notifications across SMS, WhatsApp, and Email.
/// Resolves templates, replaces placeholders, logs delivery, and queues async sends.
/// </summary>
public sealed class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly SmsService _sms;
    private readonly WhatsAppService _whatsApp;
    private readonly EmailQueueService _emailQueue;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(ApplicationDbContext db, SmsService sms, WhatsAppService whatsApp,
        EmailQueueService emailQueue, ILogger<NotificationService> log)
    {
        _db = db;
        _sms = sms;
        _whatsApp = whatsApp;
        _emailQueue = emailQueue;
        _log = log;
    }

    /// <summary>Send a notification using a template, replacing placeholders with values.</summary>
    public async Task<Guid> SendFromTemplateAsync(Guid templateId, string recipient,
        Dictionary<string, string> placeholders, Guid? customerId = null,
        string? refType = null, string? refId = null)
    {
        var template = await _db.NotificationTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.NotificationTemplateId == templateId && t.IsActive);

        if (template is null)
            throw new InvalidOperationException("Notification template not found or inactive.");

        var body = ReplacePlaceholders(template.Body, placeholders);
        var subject = template.Subject != null ? ReplacePlaceholders(template.Subject, placeholders) : null;

        return await SendAsync(template.Channel, recipient, subject, body, customerId, templateId, refType, refId);
    }

    /// <summary>Send a direct notification (no template).</summary>
    public async Task<Guid> SendAsync(string channel, string recipient, string? subject, string body,
        Guid? customerId = null, Guid? templateId = null, string? refType = null, string? refId = null)
    {
        var log = new NotificationLog
        {
            NotificationLogId = Guid.NewGuid(),
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = "Queued",
            CustomerId = customerId,
            TemplateId = templateId,
            RefType = refType,
            RefId = refId,
            SentAtUtc = DateTime.UtcNow
        };

        _db.NotificationLogs.Add(log);
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                switch (channel.ToLower())
                {
                    case "sms":
                        var smsResult = await _sms.SendAsync(recipient, body);
                        await UpdateLogStatusAsync(log.NotificationLogId,
                            smsResult.Success ? "Sent" : "Failed",
                            smsResult.Error, smsResult.MessageId);
                        break;

                    case "whatsapp":
                        var waResult = await _whatsApp.SendTextAsync(recipient, body);
                        await UpdateLogStatusAsync(log.NotificationLogId,
                            waResult.Success ? "Sent" : "Failed",
                            waResult.Error, waResult.MessageId);
                        break;

                    case "email":
                        await _emailQueue.EnqueueAsync(recipient, subject ?? "RetailERP Notification", body);
                        await UpdateLogStatusAsync(log.NotificationLogId, "Sent", null, null);
                        break;

                    default:
                        await UpdateLogStatusAsync(log.NotificationLogId, "Failed", $"Unknown channel: {channel}", null);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[Notification] Failed to send {Channel} to {Recipient}", channel, recipient);
                try { await UpdateLogStatusAsync(log.NotificationLogId, "Failed", ex.Message, null); } catch { }
            }
        });

        return log.NotificationLogId;
    }

    /// <summary>Send bill receipt to customer via their preferred/available channels.</summary>
    public async Task SendBillReceiptAsync(PosBill bill, Customer? customer, string storeName)
    {
        if (customer is null) return;

        var placeholders = new Dictionary<string, string>
        {
            { "CustomerName", customer.Name },
            { "BillNo", bill.BillNo },
            { "Amount", bill.GrandTotal.ToString("N2") },
            { "StoreName", storeName },
            { "Date", bill.BillDate.ToString("dd-MMM-yyyy") },
            { "ItemCount", bill.Lines.Count.ToString() }
        };

        var receiptBody = $"Dear {customer.Name},\n\n" +
            $"Thank you for shopping at {storeName}!\n\n" +
            $"Bill No: {bill.BillNo}\n" +
            $"Date: {bill.BillDate:dd-MMM-yyyy}\n" +
            $"Items: {bill.Lines.Count}\n" +
            $"Total: ₹{bill.GrandTotal:N2}\n\n" +
            $"Thank you for your purchase!";

        var htmlReceipt = $"<h3>Thank you for shopping at {storeName}!</h3>" +
            $"<table style='border-collapse:collapse;width:100%;max-width:400px;'>" +
            $"<tr><td style='padding:4px;'><strong>Bill No</strong></td><td>{bill.BillNo}</td></tr>" +
            $"<tr><td style='padding:4px;'><strong>Date</strong></td><td>{bill.BillDate:dd-MMM-yyyy}</td></tr>" +
            $"<tr><td style='padding:4px;'><strong>Items</strong></td><td>{bill.Lines.Count}</td></tr>" +
            $"<tr style='font-size:1.2em;'><td style='padding:4px;'><strong>Total</strong></td><td><strong>₹{bill.GrandTotal:N2}</strong></td></tr>" +
            $"</table><br/><p>Thank you for your purchase, {customer.Name}!</p>";

        if (!string.IsNullOrWhiteSpace(customer.Phone))
        {
            try
            {
                await SendAsync("Sms", customer.Phone, null, receiptBody,
                    customer.CustomerId, null, "PosBill", bill.PosBillId.ToString());
            }
            catch (Exception ex) { _log.LogWarning(ex, "SMS receipt failed for bill {BillNo}", bill.BillNo); }

            try
            {
                await SendAsync("WhatsApp", customer.Phone, null, receiptBody,
                    customer.CustomerId, null, "PosBill", bill.PosBillId.ToString());
            }
            catch (Exception ex) { _log.LogWarning(ex, "WhatsApp receipt failed for bill {BillNo}", bill.BillNo); }
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            try
            {
                await SendAsync("Email", customer.Email, $"Receipt - {bill.BillNo} | {storeName}", htmlReceipt,
                    customer.CustomerId, null, "PosBill", bill.PosBillId.ToString());
            }
            catch (Exception ex) { _log.LogWarning(ex, "Email receipt failed for bill {BillNo}", bill.BillNo); }
        }
    }

    /// <summary>Send a promotional campaign to multiple customers.</summary>
    public async Task<CampaignResult> SendCampaignAsync(string channel, string subject, string body,
        List<Customer> recipients)
    {
        var result = new CampaignResult();

        foreach (var customer in recipients)
        {
            var recipient = channel.ToLower() switch
            {
                "email" => customer.Email,
                _ => customer.Phone
            };

            if (string.IsNullOrWhiteSpace(recipient)) { result.Skipped++; continue; }

            var personalBody = body
                .Replace("{CustomerName}", customer.Name)
                .Replace("{Phone}", customer.Phone ?? "")
                .Replace("{Email}", customer.Email ?? "");

            try
            {
                await SendAsync(channel, recipient, subject, personalBody, customer.CustomerId, null, "Campaign", null);
                result.Sent++;
            }
            catch { result.Failed++; }
        }

        return result;
    }

    private async Task UpdateLogStatusAsync(Guid logId, string status, string? error, string? externalId)
    {
        var log = await _db.NotificationLogs.FindAsync(logId);
        if (log is null) return;
        log.Status = status;
        log.ErrorMessage = error;
        log.ExternalId = externalId;
        await _db.SaveChangesAsync();
    }

    private static string ReplacePlaceholders(string text, Dictionary<string, string> values)
    {
        foreach (var (key, val) in values)
            text = text.Replace($"{{{key}}}", val);
        return text;
    }

    public sealed class CampaignResult
    {
        public int Sent { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
    }
}

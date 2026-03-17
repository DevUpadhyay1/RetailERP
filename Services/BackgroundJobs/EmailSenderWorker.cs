using Microsoft.AspNetCore.Identity.UI.Services;

namespace RetailERP.Services.BackgroundJobs;

/// <summary>
/// Sprint 9: Background worker that drains the email queue and sends emails via SMTP.
/// Moves email sending out of the HTTP request pipeline for better responsiveness.
/// </summary>
public sealed class EmailSenderWorker : BackgroundService
{
    private readonly EmailQueueService _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSenderWorker> _log;

    public EmailSenderWorker(EmailQueueService queue, IServiceScopeFactory scopeFactory,
                             ILogger<EmailSenderWorker> log)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Sprint 9: EmailSenderWorker started");

        await foreach (var email in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendEmailAsync(email.To, email.Subject, email.HtmlBody);
                _log.LogInformation("Email sent to {To}: {Subject}", email.To, email.Subject);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send email to {To}: {Subject}", email.To, email.Subject);
            }
        }
    }
}

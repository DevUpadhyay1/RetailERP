using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RetailERP.Services;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;

    public string User { get; set; } = "";
    public string Password { get; set; } = "";

    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "RetailERP";
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opts;

    public SmtpEmailSender(IOptions<SmtpOptions> opts)
    {
        _opts = opts.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var fromEmail = string.IsNullOrWhiteSpace(_opts.FromEmail) ? _opts.User : _opts.FromEmail;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opts.FromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

        using var client = new SmtpClient();
        var secure = _opts.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

        await client.ConnectAsync(_opts.Host, _opts.Port, secure);
        await client.AuthenticateAsync(_opts.User, _opts.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}


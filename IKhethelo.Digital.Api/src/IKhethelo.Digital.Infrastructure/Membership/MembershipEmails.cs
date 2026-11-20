using System.Net;
using System.Net.Mail;
using System.Globalization;
using IKhethelo.Digital.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class EmailSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Es'hlahleni Social Club";
}

public sealed class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender
{
    public bool Enabled => options.Value.Enabled;
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            Timeout = 15000
        };
        using var message = new MailMessage { From = new MailAddress(settings.FromAddress, settings.FromName), Subject = subject, Body = htmlBody, IsBodyHtml = true };
        message.To.Add(to);
        await client.SendMailAsync(message, cancellationToken);
    }
}

// Best-effort notification after commit. Failures never change the saved business result.
public sealed class MembershipEmails(IEmailSender sender, ILogger<MembershipEmails> logger)
{
    public Task ReceivedAsync(string to, string firstName, Guid applicationId, DateTimeOffset submittedAtUtc)
    {
        var submitted = submittedAtUtc.ToOffset(TimeSpan.FromHours(2)).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        var html = $"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;background:#f3f1e9;color:#111410;font-family:Arial,sans-serif">
            <table role="presentation" style="width:100%;border-collapse:collapse"><tr><td style="padding:24px 12px">
            <table role="presentation" style="width:100%;max-width:560px;margin:auto;border-collapse:collapse;background:#fff">
            <tr><td style="padding:32px;background:#1c201a;color:#f3f1e9">
            <p style="font-size:11px;letter-spacing:2px;color:#bdc99c">YOUR FIRST STEP IS COMPLETE</p>
            <h1 style="font-size:40px;line-height:1.05;margin:24px 0">APPLICATION<br>SUBMITTED.</h1>
            <p style="margin:0;color:#bdc99c;font-weight:bold">Pending review</p></td></tr>
            <tr><td style="padding:32px;line-height:1.7">
            <p>Hi {WebUtility.HtmlEncode(firstName)},</p><p><strong>Your application has been submitted.</strong></p>
            <p>Thanks for applying to Es’hlahleni Social Club. Your application will be reviewed by an administrator.</p>
            <p>32 Albany Grove<br>Durban Central<br>Durban<br>4001</p>
            <p style="margin:28px 0"><a href="https://wa.me/27837265910" style="display:inline-block;background:#bdc99c;color:#111410;padding:14px 18px;text-decoration:none;font-weight:bold">Chat on WhatsApp · 083 726 5910</a></p>
            <div style="background:#f3f1e9;padding:20px"><p style="font-size:11px;letter-spacing:1px;margin:0 0 12px">YOUR APPLICATION REFERENCE</p>
            <p style="font-family:Consolas,monospace;font-size:14px;overflow-wrap:anywhere;word-break:break-all;margin:0">{applicationId:D}</p>
            <p style="font-size:12px;color:#656b60;margin:16px 0 0">Submitted {submitted} SAST</p></div>
            <p style="font-size:13px;color:#656b60">Keep this reference for any enquiry with the club. There is no public application-tracking or member-login page.</p>
            </td></tr></table></td></tr></table></body></html>
            """;
        return SendHtmlAsync(to, "Application submitted — Es’hlahleni Social Club", html);
    }

    public Task<bool> ActivatedAsync(string to, string firstName, string memberNumber) => DeliverAsync(to, "Your Es'hlahleni Membership Is Confirmed", firstName,
        "Your membership application to Es'hlahleni Social Club has been accepted. Keep this number for your membership records.", memberNumber);

    private async Task<bool> DeliverAsync(string to, string subject, string firstName, string body, string? number = null)
    {
        var html = $"""
            <!doctype html><html><head><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="margin:0;background:#eee;color:#111;font-family:Arial,sans-serif">
            <main style="max-width:560px;margin:24px auto;padding:28px;background:#fff;border-top:8px solid #111">
            <h1 style="font-size:24px">{WebUtility.HtmlEncode(subject)}</h1><p>Hi {WebUtility.HtmlEncode(firstName)},</p>
            <p style="line-height:1.6">{WebUtility.HtmlEncode(body)}</p>
            {(number is null ? "" : $"<p>Member Number: <strong>{WebUtility.HtmlEncode(number)}</strong></p>")}
            <p>32 Albany Grove<br>Durban Central, Durban, 4001</p>
            <p>WhatsApp: <a href="https://wa.me/27837265910">083 726 5910</a></p>
            <p>KUPHOLIWE AK'TATAZELWA!</p><strong>Es'hlahleni Social Club</strong></main></body></html>
            """;
        return await SendHtmlAsync(to, subject, html);
    }

    private async Task<bool> SendHtmlAsync(string to, string subject, string html)
    {
        try
        {
            if (sender is SmtpEmailSender { Enabled: false }) return false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await sender.SendAsync(to, subject, html, timeout.Token);
            return true;
        }
        catch (Exception error)
        {
            // Do not log provider exceptions: their messages can contain recipient/account data.
            logger.LogWarning("Membership email delivery failed ({FailureType}); database operation remains committed", error.GetType().Name);
            return false;
        }
    }
}

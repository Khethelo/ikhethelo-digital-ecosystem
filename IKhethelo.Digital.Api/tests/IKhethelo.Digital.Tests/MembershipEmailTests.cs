using IKhethelo.Digital.Application;
using IKhethelo.Digital.Infrastructure.Membership;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class RecordingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Html)> Messages { get; } = [];
    public bool Fail { get; set; }
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        Messages.Add((to, subject, htmlBody));
        return Fail ? Task.FromException(new InvalidOperationException("Simulated provider failure")) : Task.CompletedTask;
    }
}

public sealed class MembershipEmailTests
{
    [Fact]
    public async Task DisabledEmailReportsNotSent()
    {
        var sender = new SmtpEmailSender(Options.Create(new EmailSettings { Enabled = false }));
        var emails = new MembershipEmails(sender, NullLogger<MembershipEmails>.Instance);
        Assert.False(await emails.ActivatedAsync("person@example.test", "Person", "ESC-00123"));
    }
    [Fact]
    public async Task RegistrationEmailUsesConfirmedDetailsAndDoesNotClaimActiveMembership()
    {
        var sender = new RecordingEmailSender();
        var emails = new MembershipEmails(sender, NullLogger<MembershipEmails>.Instance);
        var reference = Guid.NewGuid();
        await emails.ReceivedAsync("person@example.test", "<Person>", reference,
            new DateTimeOffset(2026, 11, 10, 18, 2, 0, TimeSpan.Zero));
        var message = Assert.Single(sender.Messages);
        Assert.Equal("person@example.test", message.To);
        Assert.Contains("32 Albany Grove", message.Html);
        Assert.Contains("Durban Central<br>Durban<br>4001", message.Html);
        Assert.Contains(reference.ToString("D"), message.Html);
        Assert.Contains("Submitted 10 Nov 2026, 20:02 SAST", message.Html);
        Assert.Contains("Pending review", message.Html);
        Assert.Contains("YOUR FIRST STEP IS COMPLETE", message.Html);
        Assert.Contains("no public application-tracking or member-login page", message.Html);
        Assert.Contains("https://wa.me/27837265910", message.Html);
        Assert.Contains("&lt;Person&gt;", message.Html);
        Assert.DoesNotContain("membership is now active", message.Html);
        Assert.DoesNotContain("Member Number:", message.Html);
    }

    [Fact]
    public async Task RegistrationDeliveryFailureDoesNotEscapeAndSastDateCanRollOver()
    {
        var sender = new RecordingEmailSender { Fail = true };
        var emails = new MembershipEmails(sender, NullLogger<MembershipEmails>.Instance);
        await emails.ReceivedAsync("person@example.test", "Person", Guid.NewGuid(),
            new DateTimeOffset(2026, 11, 10, 23, 30, 0, TimeSpan.Zero));
        Assert.Contains("Submitted 11 Nov 2026, 01:30 SAST", Assert.Single(sender.Messages).Html);
    }

    [Fact]
    public async Task ActivationEmailContainsActualNumberAndFailureDoesNotEscape()
    {
        var sender = new RecordingEmailSender { Fail = true };
        var emails = new MembershipEmails(sender, NullLogger<MembershipEmails>.Instance);
        Assert.False(await emails.ActivatedAsync("person@example.test", "Person", "ESC-00123"));
        var message = Assert.Single(sender.Messages);
        Assert.Contains("ESC-00123", message.Html); Assert.Contains("has been accepted", message.Html);
    }
}

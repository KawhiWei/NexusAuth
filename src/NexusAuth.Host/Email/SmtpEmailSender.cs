using System.Net.Mail;
using Microsoft.Extensions.Options;
using MimeKit;

namespace NexusAuth.Host.Email;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string textBody, CancellationToken ct = default);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ISmtpTransport transport) : IEmailSender
{
    public Task SendAsync(string to, string subject, string textBody, CancellationToken ct = default)
    {
        var smtp = options.Value;
        if (!smtp.Enabled)
            throw new InvalidOperationException("SMTP email sending is not enabled.");

        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(textBody);
        if (!MailAddress.TryCreate(to, out var recipient) || recipient.Address != to)
            throw new ArgumentException("A single recipient email address is required.", nameof(to));
        if (subject.Contains('\r') || subject.Contains('\n'))
            throw new ArgumentException("The subject must be a single line.", nameof(subject));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(new MailboxAddress("", recipient.Address));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = textBody };
        return transport.SendAsync(smtp, message, ct);
    }
}

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NexusAuth.Host.Email;

public interface ISmtpTransport
{
    Task SendAsync(SmtpOptions options, MimeMessage message, CancellationToken ct = default);
}

public sealed class MailKitSmtpTransport : ISmtpTransport
{
    public async Task SendAsync(SmtpOptions options, MimeMessage message, CancellationToken ct = default)
    {
        var security = options.Security switch
        {
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            "StartTls" => SecureSocketOptions.StartTls,
            _ => throw new InvalidOperationException("SMTP requires TLS."),
        };

        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(options.Host, options.Port, security, ct);
        await client.AuthenticateAsync(options.Username, options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

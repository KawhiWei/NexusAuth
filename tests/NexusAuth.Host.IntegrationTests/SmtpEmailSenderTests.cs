using Microsoft.Extensions.Options;
using System.Text.Json;
using MimeKit;
using NexusAuth.Host.Email;
using Xunit;
using Xunit.Abstractions;

namespace NexusAuth.Host.IntegrationTests;

public sealed class SmtpEmailSenderTests(ITestOutputHelper output)
{
    [Fact]
    public void Disabled_smtp_does_not_require_connection_settings()
    {
        var result = new SmtpOptionsValidator().Validate(null, new SmtpOptions { Enabled = false });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Enabled_smtp_requires_connection_settings()
    {
        var result = new SmtpOptionsValidator().Validate(null, new SmtpOptions { Enabled = true });

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(465, "SslOnConnect")]
    [InlineData(587, "StartTls")]
    public void Enabled_smtp_accepts_supported_tls_modes(int port, string security)
    {
        var result = new SmtpOptionsValidator().Validate(null, ValidOptions(port, security));

        Assert.False(result.Failed);
    }

    [Fact]
    public async Task Send_creates_message_with_configured_sender_and_plain_text_body()
    {
        var options = ValidOptions(587, "StartTls");
        var transport = new RecordingTransport();
        var sender = new SmtpEmailSender(Options.Create(options), transport);

        await sender.SendAsync("recipient@example.test", "Test subject", "Test body");

        Assert.Same(options, transport.Options);
        var message = Assert.IsType<MimeMessage>(transport.Message);
        var from = Assert.Single(message.From.Mailboxes);
        Assert.Equal("NexusAuth", from.Name);
        Assert.Equal("sender@example.test", from.Address);
        Assert.Equal("recipient@example.test", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("Test subject", message.Subject);
        Assert.Equal("Test body", message.TextBody);
    }

    [Fact]
    public async Task Disabled_smtp_does_not_invoke_transport()
    {
        var transport = new RecordingTransport();
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions { Enabled = false }), transport);

        await Assert.ThrowsAnyAsync<Exception>(() => sender.SendAsync("recipient@example.test", "Subject", "Body"));

        Assert.Null(transport.Message);
    }

    [Fact]
    public async Task Configured_live_qq_smtp_can_send_test_message()
    {
        var localConfig = Path.Combine(AppContext.BaseDirectory, "smtp.qq.local.json");
        if (!File.Exists(localConfig))
        {
            output.WriteLine("未发送：找不到 smtp.qq.local.json。");
            return;
        }

        var credentials = JsonSerializer.Deserialize<QqSmtpCredentials>(await File.ReadAllTextAsync(localConfig));
        if (credentials is null
            || string.IsNullOrWhiteSpace(credentials.Username)
            || credentials.Username == "your-qq-number@qq.com"
            || string.IsNullOrWhiteSpace(credentials.AuthorizationCode))
        {
            output.WriteLine("未发送：请在 smtp.qq.local.json 中填写 QQ 发件邮箱和 SMTP 授权码。");
            return;
        }

        var options = new SmtpOptions
        {
            Enabled = true,
            Host = "smtp.qq.com",
            Port = 465,
            Security = "SslOnConnect",
            Username = credentials.Username,
            Password = credentials.AuthorizationCode,
            FromAddress = credentials.Username,
            FromName = "NexusAuth",
        };
        var validation = new SmtpOptionsValidator().Validate(null, options);
        Assert.False(validation.Failed, string.Join("; ", validation.Failures ?? []));

        var sender = new SmtpEmailSender(Options.Create(options), new MailKitSmtpTransport());
        await sender.SendAsync("18790997531@163.com", "NexusAuth QQ SMTP test", "NexusAuth QQ SMTP delivery test.");
        output.WriteLine("QQ SMTP 已完成发送，收件人：18790997531@163.com。");
    }

    private sealed record QqSmtpCredentials(string Username, string AuthorizationCode);

    private static SmtpOptions ValidOptions(int port, string security) => new()
    {
        Enabled = true,
        Host = "smtp.example.test",
        Port = port,
        Username = "smtp-user",
        Password = "test-placeholder-not-a-secret",
        FromAddress = "sender@example.test",
        FromName = "NexusAuth",
        Security = security,
    };

    private sealed class RecordingTransport : ISmtpTransport
    {
        public SmtpOptions? Options { get; private set; }
        public MimeMessage? Message { get; private set; }

        public Task SendAsync(SmtpOptions options, MimeMessage message, CancellationToken ct = default)
        {
            Options = options;
            Message = message;
            return Task.CompletedTask;
        }
    }
}

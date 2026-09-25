using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace NexusAuth.Host.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 465;
    public string Security { get; set; } = "SslOnConnect";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "NexusAuth";
}

public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host))
            errors.Add("Smtp:Host is required.");
        if (options.Port is < 1 or > 65535)
            errors.Add("Smtp:Port must be between 1 and 65535.");
        if (options.Security is not ("SslOnConnect" or "StartTls"))
            errors.Add("Smtp:Security must be SslOnConnect or StartTls.");
        if (string.IsNullOrWhiteSpace(options.Username))
            errors.Add("Smtp:Username is required.");
        if (string.IsNullOrWhiteSpace(options.Password))
            errors.Add("Smtp:Password is required.");
        if (string.IsNullOrWhiteSpace(options.FromAddress)
            || !MailAddress.TryCreate(options.FromAddress, out var from)
            || from.Address != options.FromAddress)
            errors.Add("Smtp:FromAddress must be a single email address.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}

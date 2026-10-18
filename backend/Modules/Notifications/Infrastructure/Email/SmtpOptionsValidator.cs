using Microsoft.Extensions.Options;
using MimeKit;

namespace UGTM.Modules.Notifications.Infrastructure.Email;

public sealed class SmtpOptionsValidator(bool allowUnencrypted)
    : IValidateOptions<SmtpOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("SMTP host is required.");
        }

        if (options.Port is <= 0 or > 65535)
        {
            failures.Add("SMTP port must be between 1 and 65535.");
        }

        if (!IsMailbox(options.FromAddress))
        {
            failures.Add("SMTP from address must be a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(options.FromName))
        {
            failures.Add("SMTP from name is required.");
        }

        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        if (hasUsername != hasPassword)
        {
            failures.Add("SMTP username and password must be configured together.");
        }

        if (!Enum.IsDefined(options.Security))
        {
            failures.Add("SMTP security mode must be None, StartTls, or SslOnConnect.");
        }
        else if (!allowUnencrypted && options.Security == SmtpSecurityMode.None)
        {
            failures.Add("Unencrypted SMTP is only allowed in Development.");
        }

        if (options.TimeoutSeconds is < 5 or > 120)
        {
            failures.Add("SMTP timeout must be between 5 and 120 seconds.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsMailbox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var mailbox = MailboxAddress.Parse(value);
            return string.Equals(mailbox.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch (ParseException)
        {
            return false;
        }
    }
}

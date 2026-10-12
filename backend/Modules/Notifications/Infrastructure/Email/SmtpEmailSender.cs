using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Notifications.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public required string Host { get; init; }
    public int Port { get; init; } = 587;
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool UseStartTls { get; init; } = true;
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var password = settings.Password
                ?? throw new InvalidOperationException(
                    "SMTP password is required when a username is configured.");

            await client.AuthenticateAsync(
                settings.Username,
                password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}

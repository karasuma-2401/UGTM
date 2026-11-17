using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Notifications.Infrastructure.Email;

public enum SmtpSecurityMode
{
    None,
    StartTls,
    SslOnConnect
}

public sealed record SmtpOptions
{
    public const string SectionName = "Notifications:Smtp";

    public required string Host { get; init; }
    public int Port { get; init; } = 587;
    public required string FromAddress { get; init; }
    public required string FromName { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public SmtpSecurityMode Security { get; init; } = SmtpSecurityMode.StartTls;
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        logger.LogInformation("SMTP: Bắt đầu gửi email đến {To} | Tiêu đề: '{Subject}'", email.To, email.Subject);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.TextBody
        };
        foreach (var inlineImage in email.InlineImages ?? [])
        {
            var resource = bodyBuilder.LinkedResources.Add(
                inlineImage.FileName,
                inlineImage.Content,
                ContentType.Parse(inlineImage.MediaType));
            resource.ContentId = inlineImage.ContentId;
            resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
            if (resource is MimePart mimePart)
            {
                mimePart.ContentTransferEncoding = ContentEncoding.Base64;
            }
        }

        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            client.Timeout = checked(settings.TimeoutSeconds * 1000);
            client.CheckCertificateRevocation = true;
            await client.ConnectAsync(
                settings.Host,
                settings.Port,
                settings.Security switch
                {
                    SmtpSecurityMode.None => SecureSocketOptions.None,
                    SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
                    SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
                    _ => throw new InvalidOperationException("Unsupported SMTP security mode.")
                },
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
            logger.LogInformation("SMTP: Gửi email thành công đến {To}!", email.To);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP: Lỗi khi gửi email đến {To}: {ErrorMessage}", email.To, ex.Message);
            throw;
        }
    }
}

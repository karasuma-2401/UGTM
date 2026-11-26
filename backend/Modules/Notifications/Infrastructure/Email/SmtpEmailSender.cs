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

internal interface ISmtpTransport : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(
        string host,
        int port,
        SecureSocketOptions security,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);

    Task AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken);

    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);

    Task DisconnectAsync(bool quit, CancellationToken cancellationToken);
}

internal sealed class MailKitSmtpTransport : ISmtpTransport
{
    private readonly SmtpClient _client = new()
    {
        CheckCertificateRevocation = true
    };

    public bool IsConnected => _client.IsConnected;

    public Task ConnectAsync(
        string host,
        int port,
        SecureSocketOptions security,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        _client.Timeout = timeoutMilliseconds;
        return _client.ConnectAsync(host, port, security, cancellationToken);
    }

    public Task AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
        => _client.AuthenticateAsync(username, password, cancellationToken);

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        => _client.SendAsync(message, cancellationToken);

    public Task DisconnectAsync(bool quit, CancellationToken cancellationToken)
        => _client.DisconnectAsync(quit, cancellationToken);

    public void Dispose() => _client.Dispose();
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Func<ISmtpTransport> _transportFactory;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger)
        : this(options, logger, static () => new MailKitSmtpTransport())
    {
    }

    internal SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger,
        Func<ISmtpTransport> transportFactory)
    {
        _options = options;
        _logger = logger;
        _transportFactory = transportFactory;
    }

    public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        _logger.LogInformation("SMTP: Bắt đầu gửi email đến {To} | Tiêu đề: '{Subject}'", email.To, email.Subject);

        var message = BuildMimeMessage(settings, email);

        try
        {
            using var transport = _transportFactory();
            await ConnectAndAuthenticateAsync(transport, settings, cancellationToken);
            await transport.SendAsync(message, cancellationToken);
            await transport.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("SMTP: Gửi email thành công đến {To}!", email.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP: Lỗi khi gửi email đến {To}: {ErrorMessage}", email.To, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<EmailBatchItemResult>> SendBatchAsync(
        IReadOnlyList<EmailBatchItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return [];
        }

        var settings = _options.Value;
        var results = new EmailBatchItemResult?[items.Count];
        var prepared = new List<(int Index, EmailBatchItem Item, MimeMessage Mime)>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];
            try
            {
                var mime = BuildMimeMessage(settings, item.Message);
                prepared.Add((i, item, mime));
            }
            catch (Exception ex) when (ex is ParseException or FormatException or ArgumentException)
            {
                _logger.LogWarning(
                    ex,
                    "SMTP Batch: Địa chỉ nhận hoặc nội dung không hợp lệ cho bản ghi {DeliveryId}.",
                    item.Id);
                results[i] = new EmailBatchItemResult(
                    item.Id,
                    Succeeded: false,
                    IsPermanentFailure: true,
                    ErrorCode: "InvalidEmailFormat",
                    ErrorMessage: TruncateError(ex.Message));
            }
        }

        if (prepared.Count == 0)
        {
            return results.Select(r => r!).ToArray();
        }

        _logger.LogInformation(
            "SMTP Batch: Bắt đầu gửi lô {Count} email qua kết nối dùng chung.",
            prepared.Count);

        ISmtpTransport? transport = null;
        try
        {
            for (var p = 0; p < prepared.Count; p++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (index, item, mime) = prepared[p];

                if (transport is null || !transport.IsConnected)
                {
                    transport?.Dispose();
                    transport = _transportFactory();
                    try
                    {
                        await ConnectAndAuthenticateAsync(transport, settings, cancellationToken);
                    }
                    catch (Exception connectEx) when (connectEx is not OperationCanceledException)
                    {
                        _logger.LogError(
                            connectEx,
                            "SMTP Batch: Không thể kết nối/xác thực máy chủ SMTP. Đánh dấu tạm lỗi cho {Remaining} email còn lại.",
                            prepared.Count - p);

                        var errorCode = ClassifyErrorCode(connectEx);
                        var errorMessage = TruncateError(connectEx.Message);
                        for (var r = p; r < prepared.Count; r++)
                        {
                            var remaining = prepared[r];
                            results[remaining.Index] = new EmailBatchItemResult(
                                remaining.Item.Id,
                                Succeeded: false,
                                IsPermanentFailure: false,
                                ErrorCode: errorCode,
                                ErrorMessage: errorMessage);
                        }

                        transport.Dispose();
                        transport = null;
                        break;
                    }
                }

                try
                {
                    await transport.SendAsync(mime, cancellationToken);
                    results[index] = new EmailBatchItemResult(item.Id, Succeeded: true);
                }
                catch (SmtpCommandException smtpEx)
                {
                    var isPermanent = smtpEx.ErrorCode == SmtpErrorCode.RecipientNotAccepted
                                      && (int)smtpEx.StatusCode is 550 or 551 or 553;
                    _logger.LogWarning(
                        smtpEx,
                        "SMTP Batch: Lỗi lệnh SMTP ({StatusCode}) cho bản ghi {DeliveryId} (Permanent={IsPermanent}).",
                        (int)smtpEx.StatusCode,
                        item.Id,
                        isPermanent);

                    results[index] = new EmailBatchItemResult(
                        item.Id,
                        Succeeded: false,
                        IsPermanentFailure: isPermanent,
                        ErrorCode: $"Smtp{(int)smtpEx.StatusCode}",
                        ErrorMessage: TruncateError(smtpEx.Message));
                }
                catch (Exception sendEx) when (sendEx is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        sendEx,
                        "SMTP Batch: Lỗi truyền tải cho bản ghi {DeliveryId}. Sẽ mở lại kết nối cho thư tiếp theo.",
                        item.Id);

                    results[index] = new EmailBatchItemResult(
                        item.Id,
                        Succeeded: false,
                        IsPermanentFailure: false,
                        ErrorCode: ClassifyErrorCode(sendEx),
                        ErrorMessage: TruncateError(sendEx.Message));

                    transport.Dispose();
                    transport = null;
                }
            }
        }
        finally
        {
            if (transport is not null)
            {
                if (transport.IsConnected)
                {
                    try
                    {
                        await transport.DisconnectAsync(true, CancellationToken.None);
                    }
                    catch (Exception disconnectEx)
                    {
                        _logger.LogDebug(disconnectEx, "SMTP Batch: Bỏ qua lỗi đóng kết nối sau khi hoàn tất gửi lô.");
                    }
                }

                transport.Dispose();
            }
        }

        var succeededCount = results.Count(r => r?.Succeeded == true);
        _logger.LogInformation(
            "SMTP Batch: Hoàn tất lô gửi ({Succeeded}/{Total} thành công).",
            succeededCount,
            items.Count);

        return results.Select(r => r!).ToArray();
    }

    private static MimeMessage BuildMimeMessage(SmtpOptions settings, EmailMessage email)
    {
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
        return message;
    }

    private static async Task ConnectAndAuthenticateAsync(
        ISmtpTransport transport,
        SmtpOptions settings,
        CancellationToken cancellationToken)
    {
        var timeoutMs = checked(settings.TimeoutSeconds * 1000);
        var security = settings.Security switch
        {
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException("Unsupported SMTP security mode.")
        };

        await transport.ConnectAsync(settings.Host, settings.Port, security, timeoutMs, cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            var password = settings.Password
                ?? throw new InvalidOperationException(
                    "SMTP password is required when a username is configured.");

            await transport.AuthenticateAsync(settings.Username, password, cancellationToken);
        }
    }

    private static string ClassifyErrorCode(Exception ex)
        => ex switch
        {
            SmtpCommandException smtpEx => $"Smtp{(int)smtpEx.StatusCode}",
            AuthenticationException => "SmtpAuthFailed",
            TimeoutException => "SmtpTimeout",
            _ => ex.GetType().Name
        };

    private static string TruncateError(string message)
        => string.IsNullOrWhiteSpace(message)
            ? "SMTP delivery error."
            : message.Length <= 500
                ? message
                : message[..500];
}

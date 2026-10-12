using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Email;

namespace UGTM.Modules.Identity.Infrastructure.Notifications;

public sealed class IdentityOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityEmailOptions> emailOptions,
    ILogger<IdentityOutboxProcessor> logger) : BackgroundService
{
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessBatchAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Identity outbox batch failed."); }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();

        var now = clock.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var messages = await db.IdentityOutboxMessages
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM identity.outbox_message
                WHERE processed_at IS NULL AND next_attempt_at <= {now}
                ORDER BY created_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var protector = provider.CreateProtector(message.ProtectionPurpose);
                var json = protector.Unprotect(message.ProtectedPayload);
                var payload = JsonSerializer.Deserialize<IdentityEmailPayload>(json)
                    ?? throw new InvalidOperationException("Invalid outbox payload.");
                if (!string.Equals(message.Type, payload.Template, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Outbox message type does not match its payload.");
                }

                await sender.SendAsync(ToEmail(payload, emailOptions.Value), ct);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox message {MessageId} failed.", message.Id);
                message.MarkFailed(ex.GetType().Name, clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static EmailMessage ToEmail(
        IdentityEmailPayload payload,
        IdentityEmailOptions options)
    {
        return payload.Template switch
        {
            "activation" => new EmailMessage(
                payload.To,
                "Kích hoạt tài khoản UGTM",
                $"<p>Xin chào {System.Net.WebUtility.HtmlEncode(payload.FullName)},</p>"
                + $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(ActivationUrl(payload.Secret, options))}\">Kích hoạt tài khoản</a></p>",
                $"Kích hoạt tài khoản: {ActivationUrl(payload.Secret, options)}"),
            "recovery-otp" => new EmailMessage(
                payload.To,
                "Mã khôi phục mật khẩu UGTM",
                $"<p>Mã xác thực của bạn: <strong>{System.Net.WebUtility.HtmlEncode(payload.Secret)}</strong></p>",
                $"Mã xác thực của bạn: {payload.Secret}"),
            _ => throw new InvalidOperationException("Unknown email template.")
        };
    }

    private static string ActivationUrl(
        string token,
        IdentityEmailOptions options)
    {
        var builder = new UriBuilder(new Uri(options.PublicWebBaseUrl, "/activate"))
        {
            Fragment = $"token={token}"
        };

        return builder.Uri.AbsoluteUri;
    }
}

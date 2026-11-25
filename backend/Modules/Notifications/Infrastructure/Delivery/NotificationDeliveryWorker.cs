using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Domain.Entities;
using UGTM.Modules.Notifications.Infrastructure.Email;

namespace UGTM.Modules.Notifications.Infrastructure.Delivery;

public interface INotificationDeliveryDispatcher
{
    Task<int> DispatchNextBatchAsync(CancellationToken cancellationToken = default);
}

public sealed class NotificationDeliveryDispatcher(
    INotificationDeliveryQueueStore queueStore,
    IEmailSender emailSender,
    INotificationEmailTemplateRenderer templateRenderer,
    IOptions<NotificationDeliveryOptions> configuredOptions,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryDispatcher> logger) : INotificationDeliveryDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record EmailDeliveryPayload(
        string? Subject = null,
        string? HtmlBody = null,
        string? TextBody = null,
        string? ActionPath = null,
        string? RecipientFullName = null,
        string? CategoryLabel = null,
        string? CtaLabel = null,
        string? Notice = null,
        IReadOnlyList<NotificationEmailMetaItem>? Metadata = null,
        IReadOnlyList<NotificationEmailDigestSection>? DigestSections = null);

    public async Task<int> DispatchNextBatchAsync(CancellationToken cancellationToken = default)
    {
        var options = configuredOptions.Value;
        var batchSize = Math.Clamp(options.BatchSize, 1, 100);
        var maxAttempts = Math.Max(1, options.MaxAttempts);
        var staleMinutes = Math.Max(1, options.StaleProcessingMinutes);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var claimed = await queueStore.ClaimBatchAsync(
            batchSize,
            nowUtc,
            nowUtc.AddMinutes(-staleMinutes),
            TimeSpan.FromMinutes(staleMinutes),
            cancellationToken);

        if (claimed.Count == 0)
        {
            return 0;
        }

        var outcomes = new List<NotificationDeliveryOutcome>(claimed.Count);
        var emailBatch = new List<(ClaimedNotificationDelivery Claimed, EmailBatchItem BatchItem)>();

        foreach (var item in claimed)
        {
            if (string.Equals(item.Channel, NotificationChannels.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (TryBuildEmailBatchItem(item, out var batchItem, out var renderError))
                {
                    emailBatch.Add((item, batchItem!));
                }
                else
                {
                    outcomes.Add(new NotificationDeliveryOutcome(
                        item.DeliveryId,
                        NotificationDeliveryStatuses.FailedPermanent,
                        item.AttemptCount + 1,
                        nowUtc,
                        SentAtUtc: null,
                        ErrorCode: "InvalidDeliveryPayload",
                        ErrorMessage: renderError));
                }
            }
            else if (string.Equals(item.Channel, NotificationChannels.Fcm, StringComparison.OrdinalIgnoreCase))
            {
                if (!options.FcmEnabled)
                {
                    outcomes.Add(new NotificationDeliveryOutcome(
                        item.DeliveryId,
                        NotificationDeliveryStatuses.Skipped,
                        item.AttemptCount,
                        nowUtc,
                        SentAtUtc: null,
                        ErrorCode: "FcmDisabled",
                        ErrorMessage: null));
                }
                else
                {
                    outcomes.Add(new NotificationDeliveryOutcome(
                        item.DeliveryId,
                        NotificationDeliveryStatuses.Skipped,
                        item.AttemptCount,
                        nowUtc,
                        SentAtUtc: null,
                        ErrorCode: "FcmTransportNotConfigured",
                        ErrorMessage: null));
                }
            }
            else
            {
                outcomes.Add(new NotificationDeliveryOutcome(
                    item.DeliveryId,
                    NotificationDeliveryStatuses.FailedPermanent,
                    item.AttemptCount + 1,
                    nowUtc,
                    SentAtUtc: null,
                    ErrorCode: "UnsupportedChannel",
                    ErrorMessage: $"Unsupported notification channel '{item.Channel}'."));
            }
        }

        if (emailBatch.Count > 0)
        {
            var results = await emailSender.SendBatchAsync(
                emailBatch.Select(x => x.BatchItem).ToArray(),
                cancellationToken);
            var completedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            var resultsById = results.ToDictionary(r => r.Id);

            foreach (var (claimedItem, _) in emailBatch)
            {
                var nextAttemptCount = claimedItem.AttemptCount + 1;
                if (resultsById.TryGetValue(claimedItem.DeliveryId, out var sendResult) && sendResult.Succeeded)
                {
                    outcomes.Add(new NotificationDeliveryOutcome(
                        claimedItem.DeliveryId,
                        NotificationDeliveryStatuses.Accepted,
                        nextAttemptCount,
                        completedAtUtc,
                        SentAtUtc: completedAtUtc));
                }
                else
                {
                    var isPermanent = (sendResult?.IsPermanentFailure ?? false) || nextAttemptCount >= maxAttempts;
                    var errorCode = sendResult?.ErrorCode ?? "SmtpSendFailed";
                    var errorMessage = sendResult?.ErrorMessage ?? "Failed to deliver email notification.";

                    if (isPermanent)
                    {
                        logger.LogWarning(
                            "Notification email delivery {DeliveryId} failed permanently after {Attempts} attempt(s): {ErrorCode}.",
                            claimedItem.DeliveryId,
                            nextAttemptCount,
                            errorCode);

                        outcomes.Add(new NotificationDeliveryOutcome(
                            claimedItem.DeliveryId,
                            NotificationDeliveryStatuses.FailedPermanent,
                            nextAttemptCount,
                            completedAtUtc,
                            SentAtUtc: null,
                            ErrorCode: errorCode,
                            ErrorMessage: errorMessage));
                    }
                    else
                    {
                        var retryAtUtc = completedAtUtc.Add(ComputeBackoffDelay(nextAttemptCount));
                        outcomes.Add(new NotificationDeliveryOutcome(
                            claimedItem.DeliveryId,
                            NotificationDeliveryStatuses.RetryScheduled,
                            nextAttemptCount,
                            retryAtUtc,
                            SentAtUtc: null,
                            ErrorCode: errorCode,
                            ErrorMessage: errorMessage));
                    }
                }
            }
        }

        await queueStore.ApplyOutcomesAsync(outcomes, cancellationToken);
        return claimed.Count;
    }

    public static TimeSpan ComputeBackoffDelay(int attemptCount)
        => attemptCount switch
        {
            <= 1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(5),
            3 => TimeSpan.FromMinutes(15),
            4 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromMinutes(60)
        };

    private bool TryBuildEmailBatchItem(
        ClaimedNotificationDelivery item,
        out EmailBatchItem? batchItem,
        out string? error)
    {
        batchItem = null;
        error = null;

        if (string.IsNullOrWhiteSpace(item.Destination))
        {
            error = "Email destination is empty.";
            return false;
        }

        EmailDeliveryPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(item.PayloadJson))
        {
            try
            {
                payload = JsonSerializer.Deserialize<EmailDeliveryPayload>(item.PayloadJson, JsonOptions);
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON payload: {ex.Message}";
                return false;
            }
        }

        try
        {
            var message = templateRenderer.Render(new NotificationEmailTemplateModel(
                To: item.Destination,
                Subject: payload?.Subject ?? item.NotificationTitle,
                Title: item.NotificationTitle,
                Content: item.NotificationContent,
                NotificationType: item.NotificationType,
                Priority: item.Priority,
                RecipientFullName: payload?.RecipientFullName,
                CategoryLabel: payload?.CategoryLabel,
                ActionPath: payload?.ActionPath ?? item.ActionPath,
                CtaLabel: payload?.CtaLabel,
                Notice: payload?.Notice,
                Metadata: payload?.Metadata,
                DigestSections: payload?.DigestSections,
                PreRenderedHtmlBody: payload?.HtmlBody,
                PreRenderedTextBody: payload?.TextBody));

            batchItem = new EmailBatchItem(item.DeliveryId, message);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationDeliveryOptions> configuredOptions,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = configuredOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("NotificationDeliveryWorker đang tắt theo cấu hình Notifications:Delivery:Enabled=false.");
            return;
        }

        var intervalSeconds = Math.Max(1, options.PollIntervalSeconds);
        var batchSize = Math.Clamp(options.BatchSize, 1, 100);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                for (var pass = 0; pass < 5 && !stoppingToken.IsCancellationRequested; pass++)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDeliveryDispatcher>();
                    var processed = await dispatcher.DispatchNextBatchAsync(stoppingToken);
                    if (processed < batchSize)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NotificationDeliveryWorker gặp lỗi trong chu kỳ gửi thông báo.");
            }
        }
    }
}

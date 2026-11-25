using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.Notifications.Infrastructure.Relay;

public sealed class NotificationRelayOptions
{
    public const string SectionName = "Notifications:Relay";

    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
}

public interface INotificationEventRelayConsumer
{
    Task ConsumeAsync(
        NotificationIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationEventRelayConsumer(
    INotificationRecipientDirectory recipientDirectory,
    INotificationIngestionService ingestionService,
    ILogger<NotificationEventRelayConsumer> logger) : INotificationEventRelayConsumer
{
    public async Task ConsumeAsync(
        NotificationIntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var entry = integrationEvent.Entry
                    ?? throw new ArgumentException("Integration event entry is required.", nameof(integrationEvent));

        var resolvedRecipients = await recipientDirectory.ResolveActiveRecipientsAsync(
            entry.RecipientUserIds,
            entry.RecipientRoles,
            entry.RecipientPermissions,
            cancellationToken);

        if (resolvedRecipients.Count == 0)
        {
            return;
        }

        var excludedIds = entry.ExcludeUserIds?
            .Where(id => id != Guid.Empty)
            .ToHashSet() ?? [];

        var targetRecipients = resolvedRecipients
            .Where(r => r.UserId != Guid.Empty && !excludedIds.Contains(r.UserId))
            .DistinctBy(r => r.UserId)
            .ToArray();

        if (targetRecipients.Length == 0)
        {
            return;
        }

        var dedupKey = string.IsNullOrWhiteSpace(entry.DeduplicationKeyPrefix)
            ? $"evt:{integrationEvent.EventId:N}"
            : entry.DeduplicationKeyPrefix.Trim();

        var requests = targetRecipients
            .Select(recipient => new NotificationIngestionRequest(
                RecipientId: recipient.UserId,
                Title: entry.Title,
                Content: entry.Content,
                NotificationType: entry.NotificationType,
                SourceModule: integrationEvent.SourceModule,
                SourceEventId: integrationEvent.EventId,
                DeduplicationKey: dedupKey,
                ResourceType: entry.ResourceType,
                ResourceId: entry.ResourceId,
                AcademicBatchId: entry.AcademicBatchId,
                ActionPath: entry.ActionPath,
                Priority: entry.Priority,
                ExpiresAtUtc: entry.ExpiresAtUtc,
                EnqueuePush: entry.EnqueuePush,
                EnqueueEmail: entry.EnqueueEmail,
                RecipientEmail: recipient.Email,
                EmailSubject: entry.EmailSubject,
                RecipientFullName: recipient.FullName,
                EmailCategoryLabel: entry.EmailCategoryLabel,
                EmailCtaLabel: entry.EmailCtaLabel,
                EmailNotice: entry.EmailNotice,
                EmailMetadata: entry.EmailMetadata,
                EmailDigestSections: entry.EmailDigestSections))
            .ToArray();

        var results = await ingestionService.IngestBatchAsync(requests, cancellationToken);
        logger.LogDebug(
            "Relayed notification event {EventId} ({EventType}) from {SourceModule} to {RecipientCount} recipient(s) ({CreatedCount} created).",
            integrationEvent.EventId,
            integrationEvent.EventType,
            integrationEvent.SourceModule,
            targetRecipients.Length,
            results.Count(r => !r.Deduplicated));
    }
}

public sealed class NotificationEventRelayWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationRelayOptions> configuredOptions,
    TimeProvider timeProvider,
    ILogger<NotificationEventRelayWorker> logger) : BackgroundService
{
    private DateTime _lastHealthCheckUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = configuredOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("NotificationEventRelayWorker đang tắt theo cấu hình Notifications:Relay:Enabled=false.");
            return;
        }

        var intervalSeconds = Math.Max(1, options.PollIntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessSourcesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "NotificationEventRelayWorker gặp lỗi trong chu kỳ relay sự kiện thông báo.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down gracefully.
        }
    }

    private async Task ProcessSourcesAsync(CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(configuredOptions.Value.BatchSize, 1, 100);
        var checkHealth = timeProvider.GetUtcNow().UtcDateTime - _lastHealthCheckUtc >= TimeSpan.FromMinutes(1);

        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var sourceNames = discoveryScope.ServiceProvider
            .GetServices<INotificationIntegrationEventSource>()
            .Select(s => s.SourceName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var sourceName in sourceNames)
        {
            try
            {
                for (var pass = 0; pass < 5 && !cancellationToken.IsCancellationRequested; pass++)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var source = scope.ServiceProvider
                        .GetServices<INotificationIntegrationEventSource>()
                        .First(s => string.Equals(s.SourceName, sourceName, StringComparison.Ordinal));
                    var consumer = scope.ServiceProvider.GetRequiredService<INotificationEventRelayConsumer>();
                    var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

                    var processed = await source.ProcessBatchAsync(
                        consumer.ConsumeAsync,
                        nowUtc,
                        batchSize,
                        cancellationToken);

                    if (processed < batchSize)
                    {
                        if (checkHealth)
                        {
                            var health = await source.GetHealthAsync(cancellationToken);
                            if (health.FailedCount > 0
                                || (health.OldestPendingUtc is { } oldest && nowUtc - oldest > TimeSpan.FromMinutes(5)))
                            {
                                logger.LogWarning(
                                    "Notification outbox {SourceName}: {PendingCount} pending, {FailedCount} retrying, oldest {OldestPendingUtc}.",
                                    health.Source,
                                    health.PendingCount,
                                    health.FailedCount,
                                    health.OldestPendingUtc);
                            }
                        }

                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Notification integration source {SourceName} failed.",
                    sourceName);
            }
        }

        if (checkHealth)
        {
            _lastHealthCheckUtc = timeProvider.GetUtcNow().UtcDateTime;
        }
    }
}

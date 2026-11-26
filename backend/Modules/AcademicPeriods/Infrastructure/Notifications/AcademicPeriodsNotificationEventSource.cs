using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.AcademicPeriods.Infrastructure.Persistence;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Notifications;

public sealed class AcademicPeriodsNotificationEventSource(AcademicPeriodsDbContext db)
    : INotificationIntegrationEventSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string SourceName => AcademicPeriodsNotificationOutbox.SourceModuleName;

    public async Task<NotificationOutboxHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var pending = db.NotificationEventOutboxMessages.AsNoTracking().Where(x => x.ProcessedAt == null);
        var summary = await pending.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            Failed = g.Count(x => x.AttemptCount > 0),
            Oldest = g.Min(x => (DateTime?)x.CreatedAt)
        }).SingleOrDefaultAsync(cancellationToken);

        return new NotificationOutboxHealth(SourceName, summary?.Count ?? 0, summary?.Failed ?? 0, summary?.Oldest);
    }

    public async Task<int> ProcessBatchAsync(
        Func<NotificationIntegrationEvent, CancellationToken, Task> handler,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<int>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var messages = await db.NotificationEventOutboxMessages
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM academic.notification_event_outbox
                    WHERE processed_at IS NULL AND next_attempt_at <= {nowUtc}
                    ORDER BY created_at, id
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            foreach (var message in messages)
            {
                try
                {
                    var integrationEvent = JsonSerializer.Deserialize<NotificationIntegrationEvent>(message.PayloadJson, JsonOptions)
                        ?? throw new InvalidOperationException("Invalid notification outbox payload.");
                    if (integrationEvent.EventId != message.EventId
                        || !string.Equals(integrationEvent.EventType, message.EventType, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Notification outbox envelope does not match its payload.");
                    }

                    await handler(integrationEvent, cancellationToken);
                    message.MarkProcessed(nowUtc);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    message.MarkFailed(ex.GetType().Name, nowUtc);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return messages.Count;
        });
    }
}

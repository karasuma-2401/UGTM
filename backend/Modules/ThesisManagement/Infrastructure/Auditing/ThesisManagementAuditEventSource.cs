using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Auditing;

public sealed class ThesisManagementAuditEventSource(ThesisManagementDbContext db)
    : IAuditIntegrationEventSource
{
    public string SourceName => "ThesisManagement";

    public async Task ProcessBatchAsync(
        Func<AuditIntegrationEvent, CancellationToken, Task> handler,
        DateTime nowUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var messages = await db.AuditOutboxMessages
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM thesis.audit_outbox_message
                WHERE processed_at IS NULL AND next_attempt_at <= {nowUtc}
                ORDER BY created_at
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var integrationEvent = JsonSerializer.Deserialize<AuditIntegrationEvent>(message.PayloadJson)
                    ?? throw new InvalidOperationException("Invalid audit outbox payload.");
                if (integrationEvent.EventId != message.Id
                    || !string.Equals(integrationEvent.Action, message.Action, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Audit outbox envelope does not match its payload.");
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
    }
}

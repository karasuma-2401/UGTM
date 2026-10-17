using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Infrastructure.Persistence;

namespace UGTM.Modules.Identity.Infrastructure.Auditing;

public sealed class AuditIntegrationEventConsumer(IdentityDbContext db)
{
    public async Task ConsumeAsync(
        AuditIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (integrationEvent.EventId == Guid.Empty)
        {
            throw new InvalidOperationException("Audit integration event ID is empty.");
        }

        if (await db.AuditLogs.AnyAsync(
                x => x.IntegrationEventId == integrationEvent.EventId,
                cancellationToken))
        {
            return;
        }

        ValidateMetadata(integrationEvent.MetadataJson);
        var auditLog = AuditLog.Create(
            integrationEvent.ActorUserId,
            integrationEvent.ActorSessionId,
            integrationEvent.TargetType,
            integrationEvent.TargetId,
            integrationEvent.Action,
            integrationEvent.Outcome,
            integrationEvent.ReasonCode,
            integrationEvent.RequestId,
            integrationEvent.MetadataJson,
            integrationEvent.OccurredAtUtc,
            integrationEvent.EventId);

        db.AuditLogs.Add(auditLog);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_audit_log_integration_event_id"
            })
        {
            db.Entry(auditLog).State = EntityState.Detached;
        }
        catch
        {
            db.Entry(auditLog).State = EntityState.Detached;
            throw;
        }
    }

    private static void ValidateMetadata(string? metadataJson)
    {
        if (metadataJson is null)
        {
            return;
        }

        using var _ = JsonDocument.Parse(metadataJson);
    }
}

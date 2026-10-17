# Cross-module Audit Log

Audit events from `AcademicPeriods`, `ThesisManagement`, and `DefenseCouncil`
are delivered to the Identity module through transactional outboxes.

## Flow

```text
Business handler
  -> mutate module aggregate
  -> add AuditIntegrationEvent to the module audit outbox
  -> save business data and outbox in one transaction
  -> AuditIntegrationEventProcessor claims the outbox row
  -> Identity writes identity.audit_log
  -> source outbox row is marked processed
```

Each source uses `FOR UPDATE SKIP LOCKED`. Delivery is at-least-once. The
nullable `identity.audit_log.integration_event_id` column has a filtered unique
index, so retrying an event after a process crash does not create duplicate
audit rows.

## Producer ports

- `IAcademicPeriodsAuditOutbox`
- `IThesisManagementAuditOutbox`
- `IDefenseCouncilAuditOutbox`

The implementation captures the current actor, session, HTTP request ID, and
serializes allowlisted metadata. It only adds an outbox entity to the owning
module's `DbContext`; the business handler remains responsible for the single
`SaveChangesAsync` call that commits both changes.

Example for a future AcademicPeriods handler:

```csharp
auditOutbox.Add(
    new AuditIntegrationEntry(
        TargetType: "KltnBatch",
        TargetId: batch.Id.ToString(),
        Action: IntegrationAuditActions.BatchConfigurationUpdated,
        Outcome: "Success",
        ReasonCode: null,
        Metadata: new
        {
            ChangedFields = new[] { "startDate", "endDate" }
        }),
    nowUtc);

await unitOfWork.SaveChangesAsync(cancellationToken);
```

Do not serialize request DTOs or entities directly. Metadata must be an
explicit allowlist and must never contain passwords, tokens, credentials, or
security stamps.

## Stable action codes

- `AcademicPeriods.Batch.ConfigurationUpdated`
- `AcademicPeriods.Score.ConfigurationUpdated`
- `ThesisManagement.RenameRequest.Approved`
- `DefenseCouncil.CouncilMinute.UnlockGranted`
- `DefenseCouncil.Score.Adjusted`
- `DefenseCouncil.Score.Excluded`

The corresponding business command handlers are not present in the current
repository. When they are introduced, inject the owning producer port and add
the event immediately before the handler's single `SaveChangesAsync` call.

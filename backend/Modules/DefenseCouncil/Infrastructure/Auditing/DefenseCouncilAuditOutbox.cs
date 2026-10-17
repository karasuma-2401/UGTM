using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Auditing;

public sealed class DefenseCouncilAuditOutbox(
    DefenseCouncilDbContext db,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : IDefenseCouncilAuditOutbox
{
    public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc)
    {
        AuditIntegrationEventValidation.Validate(entry, occurredAtUtc);

        var requestId = httpContextAccessor.HttpContext?.TraceIdentifier
                        ?? Activity.Current?.TraceId.ToString()
                        ?? Guid.NewGuid().ToString("N");
        var metadataJson = entry.Metadata is null
            ? null
            : JsonSerializer.Serialize(entry.Metadata);
        var integrationEvent = new AuditIntegrationEvent(
            Guid.NewGuid(),
            currentUser.UserId,
            currentUser.SessionId,
            requestId,
            entry.TargetType,
            entry.TargetId,
            entry.Action,
            entry.Outcome,
            entry.ReasonCode,
            metadataJson,
            occurredAtUtc);

        var payloadJson = JsonSerializer.Serialize(integrationEvent);
        db.AuditOutboxMessages.Add(AuditOutboxMessage.Create(
            integrationEvent.EventId,
            integrationEvent.Action,
            payloadJson,
            occurredAtUtc));
    }

}

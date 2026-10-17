using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Auditing;

public sealed class ThesisManagementAuditOutbox(
    ThesisManagementDbContext db,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor) : IThesisManagementAuditOutbox
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

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class AuditLogWriter(
    IdentityDbContext db,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    IClock clock
) : IAuditLogWriter
{
    public void Add(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var requestId = httpContextAccessor.HttpContext?.TraceIdentifier
                      ?? Activity.Current?.TraceId.ToString()
                      ?? Guid.NewGuid().ToString("N");
        var metadataJson = entry.Metadata is null
            ? null
            : JsonSerializer.Serialize(entry.Metadata);

        var auditLog = AuditLog.Create(
            currentUser.UserId,
            currentUser.SessionId,
            entry.TargetType,
            entry.TargetId,
            entry.Action,
            entry.Outcome,
            entry.ReasonCode,
            requestId,
            metadataJson,
            clock.UtcNow);

        db.AuditLogs.Add(auditLog);
    }
}

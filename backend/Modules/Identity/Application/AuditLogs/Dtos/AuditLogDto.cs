using System.Text.Json;

namespace UGTM.Modules.Identity.Application.AuditLogs.Dtos;

public sealed record AuditLogDto(
    long Id,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorFullName,
    Guid? ActorSessionId,
    string TargetType,
    string TargetId,
    string Action,
    string Outcome,
    string? ReasonCode,
    string RequestId,
    JsonElement? Metadata,
    DateTime Timestamp);

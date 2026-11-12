namespace UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;

public sealed record GetAuditLogsQuery(
    Guid? ActorUserId = null,
    string? Action = null,
    string? TargetType = null,
    string? Outcome = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? RequestId = null,
    string? TargetId = null,
    Guid? ActorSessionId = null);

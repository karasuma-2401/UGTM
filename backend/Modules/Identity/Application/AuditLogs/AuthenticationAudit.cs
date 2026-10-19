namespace UGTM.Modules.Identity.Application.AuditLogs;

public static class AuthenticationAudit
{
    private const string UnknownTarget = "Unknown";

    public static AuditEntry ForUser(
        Guid? userId,
        string action,
        string outcome,
        string? reasonCode = null,
        object? metadata = null)
        => new(
            userId.HasValue ? "User" : "Authentication",
            userId?.ToString() ?? UnknownTarget,
            action,
            outcome,
            reasonCode,
            metadata);

    public static AuditEntry ForSession(
        Guid sessionId,
        string action,
        string outcome,
        string? reasonCode = null,
        object? metadata = null)
        => new(
            "AuthenticationSession",
            sessionId.ToString(),
            action,
            outcome,
            reasonCode,
            metadata);
}

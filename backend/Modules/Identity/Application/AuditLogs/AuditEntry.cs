namespace UGTM.Modules.Identity.Application.AuditLogs;

public sealed record AuditEntry(
    string TargetType,
    string TargetId,
    string Action,
    string Outcome,
    string? ReasonCode,
    object? Metadata);
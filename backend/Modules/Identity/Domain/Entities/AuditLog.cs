namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? ActorSessionId { get; private set; }
    public string TargetType { get; private set; } = null!;
    public string TargetId { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Outcome { get; private set; } = null!;
    public string? ReasonCode { get; private set; }
    public string RequestId { get; private set; } = null!;
    public string? MetadataJson { get; private set; }
    public DateTime Timestamp { get; private set; }

    public static AuditLog Create(
        Guid? actorUserId,
        Guid? actorSessionId,
        string targetType,
        string targetId,
        string action,
        string outcome,
        string? reasonCode,
        string requestId,
        string? metadataJson,
        DateTime timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return new AuditLog
        {
            ActorUserId = actorUserId,
            ActorSessionId = actorSessionId,
            TargetType = targetType,
            TargetId = targetId,
            Action = action,
            Outcome = outcome,
            ReasonCode = reasonCode,
            RequestId = requestId,
            MetadataJson = metadataJson,
            Timestamp = timestamp
        };
    }
}

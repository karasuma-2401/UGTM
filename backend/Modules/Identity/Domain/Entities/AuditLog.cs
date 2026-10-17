namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public Guid? IntegrationEventId { get; private set; }
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
        DateTime timestamp,
        Guid? integrationEventId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        EnsureMaxLength(targetType, 100, nameof(targetType));
        EnsureMaxLength(targetId, 100, nameof(targetId));
        EnsureMaxLength(action, 100, nameof(action));
        EnsureMaxLength(outcome, 50, nameof(outcome));
        EnsureMaxLength(reasonCode, 100, nameof(reasonCode));
        EnsureMaxLength(requestId, 100, nameof(requestId));

        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestamp));
        }

        return new AuditLog
        {
            IntegrationEventId = integrationEventId,
            ActorUserId = actorUserId,
            ActorSessionId = actorSessionId,
            TargetType = targetType.Trim(),
            TargetId = targetId.Trim(),
            Action = action.Trim(),
            Outcome = outcome.Trim(),
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim(),
            RequestId = requestId.Trim(),
            MetadataJson = metadataJson,
            Timestamp = timestamp
        };
    }

    private static void EnsureMaxLength(string? value, int maxLength, string parameterName)
    {
        if (value?.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maxLength} characters.",
                parameterName);
        }
    }
}

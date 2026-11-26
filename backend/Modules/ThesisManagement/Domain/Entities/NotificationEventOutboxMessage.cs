using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class NotificationEventOutboxMessage : Entity<Guid>
{
    private NotificationEventOutboxMessage() { }

    private NotificationEventOutboxMessage(
        Guid eventId,
        string eventType,
        string payloadJson,
        int payloadVersion,
        DateTime createdAtUtc) : base(eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event identifier must be nonempty.", nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        if (eventType.Length > 100)
        {
            throw new ArgumentException("Event type cannot exceed 100 characters.", nameof(eventType));
        }

        if (payloadVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadVersion), "Payload version must be positive.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        EventId = eventId;
        EventType = eventType.Trim();
        PayloadVersion = payloadVersion;
        PayloadJson = payloadJson;
        CreatedAt = createdAtUtc;
        NextAttemptAt = createdAtUtc;
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = null!;
    public int PayloadVersion { get; private set; } = 1;
    public string PayloadJson { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastErrorCode { get; private set; }

    public static NotificationEventOutboxMessage Create(
        Guid eventId,
        string eventType,
        string payloadJson,
        DateTime createdAtUtc,
        int payloadVersion = 1)
        => new(eventId, eventType, payloadJson, payloadVersion, createdAtUtc);

    public void MarkProcessed(DateTime nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        ProcessedAt = nowUtc;
        LastErrorCode = null;
    }

    public void MarkFailed(string errorCode, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        EnsureUtc(nowUtc, nameof(nowUtc));
        AttemptCount++;
        LastErrorCode = errorCode.Length <= 100 ? errorCode : errorCode[..100];
        NextAttemptAt = nowUtc.AddMinutes(Math.Min(Math.Pow(2, AttemptCount), 60));
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }
}

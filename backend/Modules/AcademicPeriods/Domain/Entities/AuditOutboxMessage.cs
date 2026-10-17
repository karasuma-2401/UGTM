using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class AuditOutboxMessage : Entity<Guid>
{
    private AuditOutboxMessage() { }

    private AuditOutboxMessage(
        Guid eventId,
        string action,
        string payloadJson,
        DateTime createdAtUtc) : base(eventId)
    {
        Validate(eventId, action, payloadJson, createdAtUtc);
        Action = action;
        PayloadJson = payloadJson;
        CreatedAt = createdAtUtc;
        NextAttemptAt = createdAtUtc;
    }

    public string Action { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }

    public static AuditOutboxMessage Create(
        Guid eventId,
        string action,
        string payloadJson,
        DateTime createdAtUtc)
        => new(eventId, action, payloadJson, createdAtUtc);

    public void MarkProcessed(DateTime nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        ProcessedAt = nowUtc;
        PayloadJson = "{}";
        LastError = null;
    }

    public void MarkFailed(string error, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureUtc(nowUtc, nameof(nowUtc));
        AttemptCount++;
        LastError = error.Length <= 1000 ? error : error[..1000];
        NextAttemptAt = nowUtc.AddMinutes(Math.Min(Math.Pow(2, AttemptCount), 60));
    }

    private static void Validate(
        Guid eventId,
        string action,
        string payloadJson,
        DateTime createdAtUtc)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event identifier must be nonempty.", nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        if (action.Length > 100)
        {
            throw new ArgumentException("Action cannot exceed 100 characters.", nameof(action));
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }
}

using System.Text.Json;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

/// <summary>Receipt of a successful save/submit request, committed with its assignment and audit.</summary>
public sealed class ReviewerEvaluationIdempotencyRecord
{
    private ReviewerEvaluationIdempotencyRecord() { }

    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = null!;
    public Guid IdempotencyKey { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public int ResponseStatusCode { get; private set; }
    public string ResponsePayloadJson { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public static ReviewerEvaluationIdempotencyRecord Create(
        Guid id, Guid actorId, string operation, Guid idempotencyKey, string requestFingerprint,
        int responseStatusCode, DateTime createdAtUtc)
    {
        if (id == Guid.Empty || actorId == Guid.Empty || idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException("Receipt, actor and idempotency key must be nonempty.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (operation.Length > 100 || operation != operation.Trim())
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        if (requestFingerprint.Length != 64 || !requestFingerprint.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Request fingerprint must be a SHA-256 hexadecimal value.",
                nameof(requestFingerprint));
        }
        if (responseStatusCode is not (200 or 201))
        {
            throw new ArgumentOutOfRangeException(nameof(responseStatusCode));
        }
        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Created time must be UTC.", nameof(createdAtUtc));
        }

        return new ReviewerEvaluationIdempotencyRecord
        {
            Id = id,
            ActorId = actorId,
            Operation = operation,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint.ToLowerInvariant(),
            ResponseStatusCode = responseStatusCode,
            // Completed after SaveChanges supplies xmin, before the transaction commits.
            ResponsePayloadJson = "{}",
            CreatedAtUtc = createdAtUtc
        };
    }

    public void SetResponsePayload(string responsePayloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsePayloadJson);
        using var json = JsonDocument.Parse(responsePayloadJson);
        if (json.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Response payload must be a JSON object.", nameof(responsePayloadJson));
        }
        ResponsePayloadJson = responsePayloadJson;
    }
}

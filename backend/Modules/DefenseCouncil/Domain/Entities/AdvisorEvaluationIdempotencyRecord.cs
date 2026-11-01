namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

/// <summary>
/// Stores the successful response for an advisor-evaluation write so a network retry cannot
/// create a second audit event or overwrite a newer evaluation.
/// </summary>
public sealed class AdvisorEvaluationIdempotencyRecord
{
    private const int OperationMaximumLength = 100;
    private const int FingerprintLength = 64;

    private AdvisorEvaluationIdempotencyRecord() { }

    private AdvisorEvaluationIdempotencyRecord(
        Guid id,
        Guid actorId,
        string operation,
        Guid idempotencyKey,
        string requestFingerprint,
        DateTime createdAtUtc)
    {
        EnsureNonempty(id, nameof(id));
        EnsureNonempty(actorId, nameof(actorId));
        EnsureNonempty(idempotencyKey, nameof(idempotencyKey));
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestFingerprint);
        if (operation.Length > OperationMaximumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }
        if (requestFingerprint.Length != FingerprintLength || !requestFingerprint.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Request fingerprint must be a SHA-256 hexadecimal value.",
                nameof(requestFingerprint));
        }
        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Created time must be UTC.", nameof(createdAtUtc));
        }

        Id = id;
        ActorId = actorId;
        Operation = operation;
        IdempotencyKey = idempotencyKey;
        RequestFingerprint = requestFingerprint.ToLowerInvariant();
        ResponseStatusCode = 200;
        // The transaction coordinator replaces this placeholder after EF receives the generated
        // xmin of a new or updated evaluation, before the transaction commits.
        ResponsePayloadJson = "{}";
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public string Operation { get; private set; } = null!;
    public Guid IdempotencyKey { get; private set; }
    public string RequestFingerprint { get; private set; } = null!;
    public int ResponseStatusCode { get; private set; }
    public string ResponsePayloadJson { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public static AdvisorEvaluationIdempotencyRecord Create(
        Guid id,
        Guid actorId,
        string operation,
        Guid idempotencyKey,
        string requestFingerprint,
        DateTime createdAtUtc)
        => new(id, actorId, operation, idempotencyKey, requestFingerprint, createdAtUtc);

    public void SetResponsePayload(string responsePayloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsePayloadJson);
        ResponsePayloadJson = responsePayloadJson;
    }

    private static void EnsureNonempty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be nonempty.", parameterName);
        }
    }
}

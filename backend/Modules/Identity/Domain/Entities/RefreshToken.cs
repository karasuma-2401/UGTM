using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken() { }

    private RefreshToken(
        Guid id,
        Guid sessionId,
        string selector,
        byte[] validatorHash,
        DateTime createdAt,
        DateTime expiresAt
    ) : base(id)
    {
        if (id == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new ArgumentException("Token and session identifiers must be nonempty");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        if (validatorHash.Length == 0)
        {
            throw new ArgumentException("Validator hash is required", nameof(validatorHash));
        }
        EnsureUtc(createdAt, nameof(createdAt));
        EnsureUtc(expiresAt, nameof(expiresAt));
        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Token expiry must be after creation.");
        }
        SessionId = sessionId;
        Selector = selector;
        ValidatorHash = validatorHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid SessionId { get; private set; }
    // Random Id shorter, save planinttext to lookup by unique index
    public string Selector { get; private set; } = null!;
    // hash SHA-256 of random validator have high entropy
    public byte[] ValidatorHash { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }

    public AuthenticationSession Session { get; private set; } = null!;

    public static RefreshToken Create(
        Guid sessionId,
        string selector,
        byte[] validatorHash,
        DateTime createdAt,
        DateTime expiresAt)
        => new(
            Guid.NewGuid(),
            sessionId, selector, validatorHash, createdAt, expiresAt);

    public bool IsUsable(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        return ConsumedAt is null && RevokedAt is null && now < ExpiresAt;
    }

    public void Consume(Guid replacementId, DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (replacementId == Guid.Empty)
        {
            throw new ArgumentException("Replacement identifier must be nonempty", nameof(replacementId));
        }

        if (!IsUsable(now))
        {
            throw new InvalidOperationException("Refresh token is not usable");
        }

        ConsumedAt = now;
        ReplacedByTokenId = replacementId;
    }
    public void Revoke(SessionRevocationReason reason, DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevocationReason = reason;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time mus be UTC", parameterName);
        }
    }
}

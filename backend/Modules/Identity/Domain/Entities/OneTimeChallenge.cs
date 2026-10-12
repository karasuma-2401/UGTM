using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class OneTimeChallenge : AggregateRoot<Guid>
{
    private OneTimeChallenge() { }

    private OneTimeChallenge(
        Guid id,
        Guid userId,
        ChallengePurpose purpose,
        string selector,
        byte[] secretHash,
        string destinationSnapshot,
        int maxAttempts,
        DateTime createdAt,
        DateTime expiresAt) : base(id)
    {
        if (id == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Challenge and user identifiers must be nonempty");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationSnapshot);

        if (secretHash.Length == 0 || maxAttempts <= 0)
        {
            throw new ArgumentException("Challenge secret hash and attempt limit are required");
        }
        EnsureUtc(createdAt, nameof(createdAt));
        EnsureUtc(expiresAt, nameof(expiresAt));
        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Challenge expiry must be after creation");
        }
        UserId = userId;
        Purpose = purpose;
        Selector = selector;
        SecretHash = secretHash;
        DestinationSnapshot = destinationSnapshot;
        MaxAttempts = maxAttempts;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public ChallengePurpose Purpose { get; private set; }
    public string Selector { get; private set; } = null!;
    public byte[] SecretHash { get; private set; } = [];
    public string DestinationSnapshot { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? InvalidatedAt { get; private set; }

    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime? LastSentAt { get; private set; }
    public int SendCount { get; private set; }

    public User User { get; private set; } = null!;

    public static OneTimeChallenge Create(
        Guid userId,
        ChallengePurpose purpose,
        string selector,
        byte[] secretHash,
        string destinationSnapshot,
        int maxAttempts,
        DateTime createdAt,
        DateTime expiresAt)
        => new(
            Guid.NewGuid(),
            userId, purpose, selector, secretHash, destinationSnapshot, maxAttempts, createdAt, expiresAt);

    public bool CanAttempt(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        return ConsumedAt is null
               && InvalidatedAt is null
               && AttemptCount < MaxAttempts
               && now < ExpiresAt;
    }
    public void RecordFailedAttempt(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (!CanAttempt(now))
        {
            throw new InvalidOperationException("Challenge cannot be attempted.");
        }

        AttemptCount++;
        if (AttemptCount >= MaxAttempts)
        {
            InvalidatedAt = now;
        }
    }

    public void MarkSent(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        LastSentAt = now;
        SendCount++;
    }

    public void Consume(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (!CanAttempt(now))
        {
            throw new InvalidOperationException("Challenge cannot be consumed.");
        }

        ConsumedAt = now;
    }

    public void Invalidate(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (ConsumedAt is null && InvalidatedAt is null)
        {
            InvalidatedAt = now;
        }
    }
    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }
}

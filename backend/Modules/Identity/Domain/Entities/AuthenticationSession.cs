using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class AuthenticationSession : AggregateRoot<Guid>
{
    private AuthenticationSession() {}

    private AuthenticationSession(
        Guid id,
        Guid userId,
        Guid securityStampSnapshot,
        DateTime now,
        DateTime idleExpiresAt,
        DateTime absoluteExpiresAt,
        string? userAgentSummary,
        string? ipAddressHash) : base(id)
    {
        if (id == Guid.Empty || userId == Guid.Empty || securityStampSnapshot == Guid.Empty)
        {
            throw new ArgumentException("Session identifier and security stamp mus be nonempty");
        }

        if (idleExpiresAt <= now || absoluteExpiresAt <= idleExpiresAt)
        {
            throw new ArgumentException("Session expiry values are invalid");
        }
        UserId = userId;
        SecurityStampSnapshot = securityStampSnapshot;
        CreatedAt = now;
        AuthenticationAt = now;
        LastSeenAt = now;
        IdleExpiresAt = idleExpiresAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
        UserAgentSummary = userAgentSummary;
        IpAddressHash = ipAddressHash;
        ConcurrencyStamp = Guid.NewGuid();
    }
    
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime AuthenticationAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }
    public DateTime IdleExpiresAt { get; private set; }
    public DateTime AbsoluteExpiresAt { get; private set; }
    public Guid SecurityStampSnapshot { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public SessionRevocationReason? RevocationReason { get; private set; }
    public string? UserAgentSummary { get; private set; }
    public string? IpAddressHash { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public User User { get; private set; } = null!;
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];

    public static AuthenticationSession Create(
        Guid userId,
        Guid securityStampSnapshot,
        DateTime now,
        TimeSpan idleLifetime,
        TimeSpan absoluteLifetime,
        string? userAgentSummary,
        string? ipAddressHash)
    {
        if (idleLifetime <= TimeSpan.Zero || absoluteLifetime <= idleLifetime)
        {
            throw new ArgumentException("Session Lifetimes are invalid");
        }

        return new AuthenticationSession(
            Guid.NewGuid(),
            userId,
            securityStampSnapshot,
            now,
            now.Add(idleLifetime),
            now.Add(absoluteLifetime),
            userAgentSummary,
            ipAddressHash);
    }

    public bool CanRefresh(Guid currentSecurityStamp, DateTime now)
    {
        EnsureUtc(now,  nameof(now));
        return RevokedAt is null 
            && currentSecurityStamp == SecurityStampSnapshot
            && now < IdleExpiresAt
            && now <  AbsoluteExpiresAt;
    }

    public void Touch(DateTime now, TimeSpan idleLifetime)
    {
        EnsureUtc(now, nameof(now));
        if (idleLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(idleLifetime));
        }

        if (RevokedAt is not null || now >= AbsoluteExpiresAt)
        {
            throw new InvalidOperationException("An inactive session cannot be refreshed");
        }

        LastSeenAt = now;
        var proposedIdleExpiry = now.Add(idleLifetime);
        IdleExpiresAt = proposedIdleExpiry < AbsoluteExpiresAt
            ? proposedIdleExpiry
            : AbsoluteExpiresAt;
        ConcurrencyStamp = Guid.NewGuid();
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
        ConcurrencyStamp = Guid.NewGuid();
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }

}
using System.Security.Cryptography;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Tests.Domain;

public sealed class OneTimeChallengeTests
{
    private static readonly DateTime Now =
        new(2026, 10, 12, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Failed_attempt_at_limit_invalidates_challenge()
    {
        var challenge = Create(maxAttempts: 2);

        challenge.RecordFailedAttempt(Now.AddSeconds(1));
        challenge.RecordFailedAttempt(Now.AddSeconds(2));

        Assert.Equal(2, challenge.AttemptCount);
        Assert.Equal(Now.AddSeconds(2), challenge.InvalidatedAt);
        Assert.False(challenge.CanAttempt(Now.AddSeconds(3)));
    }

    [Fact]
    public void Consume_can_only_succeed_once()
    {
        var challenge = Create();
        challenge.Consume(Now.AddSeconds(1));

        Assert.Equal(Now.AddSeconds(1), challenge.ConsumedAt);
        Assert.Throws<InvalidOperationException>(
            () => challenge.Consume(Now.AddSeconds(2)));
    }

    [Fact]
    public void Challenge_is_invalid_at_expiry_boundary()
    {
        var challenge = Create();

        Assert.False(challenge.CanAttempt(Now.AddMinutes(10)));
    }

    private static OneTimeChallenge Create(int maxAttempts = 5)
        => OneTimeChallenge.Create(
            Guid.NewGuid(),
            ChallengePurpose.ActivateAccount,
            "selector",
            RandomNumberGenerator.GetBytes(32),
            "student@ugtm.edu.vn",
            maxAttempts,
            Now,
            Now.AddMinutes(10));
}

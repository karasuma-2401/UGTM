using System.Security.Cryptography;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Tests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now =
        new(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Consume_records_time_and_replacement()
    {
        var token = Create();
        var replacementId = Guid.NewGuid();

        token.Consume(replacementId, Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), token.ConsumedAt);
        Assert.Equal(replacementId, token.ReplacedByTokenId);
        Assert.False(token.IsUsable(Now.AddMinutes(2)));
    }

    [Fact]
    public void Token_is_not_usable_at_expiry_boundary()
    {
        var token = Create();

        Assert.False(token.IsUsable(Now.AddHours(1)));
    }

    private static RefreshToken Create()
        => RefreshToken.Create(
            Guid.NewGuid(),
            "selector",
            RandomNumberGenerator.GetBytes(32),
            Now,
            Now.AddHours(1));
}

using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Tests.Domain;

public sealed class AuthenticationSessionTests
{
    private static readonly DateTime Now =
        new(2026, 10, 12, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CanRefresh_returns_false_at_idle_expiry_boundary()
    {
        var stamp = Guid.NewGuid();
        var session = Create(stamp);

        Assert.True(session.CanRefresh(stamp, Now.AddMinutes(29)));
        Assert.False(session.CanRefresh(stamp, Now.AddMinutes(30)));
    }

    [Fact]
    public void Touch_never_extends_beyond_absolute_expiry()
    {
        var session = Create(Guid.NewGuid());

        session.Touch(Now.AddMinutes(50), TimeSpan.FromMinutes(30));

        Assert.Equal(Now.AddHours(1), session.IdleExpiresAt);
    }

    [Fact]
    public void Revoke_is_idempotent_and_prevents_refresh()
    {
        var stamp = Guid.NewGuid();
        var session = Create(stamp);
        var revokedAt = Now.AddMinutes(5);

        session.Revoke(SessionRevocationReason.UserLogout, revokedAt);
        session.Revoke(SessionRevocationReason.AdministratorRevoked, revokedAt.AddMinutes(1));

        Assert.Equal(revokedAt, session.RevokedAt);
        Assert.Equal(SessionRevocationReason.UserLogout, session.RevocationReason);
        Assert.False(session.CanRefresh(stamp, revokedAt));
    }

    private static AuthenticationSession Create(Guid stamp)
        => AuthenticationSession.Create(
            Guid.NewGuid(),
            stamp,
            Now,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(1),
            null,
            null);
}

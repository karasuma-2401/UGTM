using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Tests.Domain;

public sealed class UserTests
{
    private static readonly DateTime CreatedAt =
        new(2026, 10, 12, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ProvisionStudent_creates_pending_account_without_password()
    {
        var user = CreatePendingUser();

        Assert.Equal(AccountStatus.PendingActivation, user.Status);
        Assert.Equal("student@ugtm.edu.vn", user.Email);
        Assert.Equal("STUDENT@UGTM.EDU.VN", user.NormalizedEmail);
        Assert.Equal("SV001", user.UserCode);
        Assert.Null(user.PasswordHash);
        Assert.NotEqual(Guid.Empty, user.SecurityStamp);
        Assert.NotEqual(Guid.Empty, user.ConcurrencyStamp);
        Assert.Equal(CreatedAt, user.CreatedAt);
    }

    [Fact]
    public void Activate_sets_password_and_security_timestamps()
    {
        var user = CreatePendingUser();
        var previousStamp = user.SecurityStamp;
        var now = CreatedAt.AddMinutes(5);

        user.Activate("password-hash", now);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal("password-hash", user.PasswordHash);
        Assert.Equal(now, user.EmailVerifiedAt);
        Assert.Equal(now, user.ActivatedAt);
        Assert.Equal(now, user.PasswordChangedAt);
        Assert.NotEqual(previousStamp, user.SecurityStamp);
    }

    [Fact]
    public void RecordFailedLogin_locks_at_configured_threshold()
    {
        var user = CreateActiveUser();
        var now = CreatedAt.AddMinutes(10);

        Assert.False(user.RecordFailedLogin(now, 2, TimeSpan.FromMinutes(15)));
        Assert.True(user.RecordFailedLogin(now.AddSeconds(1), 2, TimeSpan.FromMinutes(15)));

        Assert.True(user.IsLockedOut(now.AddMinutes(1)));
        Assert.Equal(0, user.AccessFailedCount);
    }

    [Fact]
    public void ChangePassword_rotates_security_stamp()
    {
        var user = CreateActiveUser();
        var previousStamp = user.SecurityStamp;
        var now = CreatedAt.AddHours(1);

        user.ChangePassword("new-hash", now);

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal(now, user.PasswordChangedAt);
        Assert.NotEqual(previousStamp, user.SecurityStamp);
    }

    private static User CreatePendingUser()
        => User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student Name",
            "SV001",
            null,
            CreatedAt);

    private static User CreateActiveUser()
    {
        var user = CreatePendingUser();
        user.Activate("password-hash", CreatedAt.AddMinutes(1));
        return user;
    }
}

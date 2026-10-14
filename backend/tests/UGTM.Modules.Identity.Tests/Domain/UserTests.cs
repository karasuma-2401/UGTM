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

    [Fact]
    public void Create_with_active_status_and_password_sets_activation_timestamps()
    {
        var now = CreatedAt.AddMinutes(2);
        var user = User.Create(
            Guid.NewGuid(),
            "lecturer@ugtm.edu.vn",
            "LECTURER@UGTM.EDU.VN",
            "Lecturer Name",
            "GV001",
            "0912345678",
            "initial-hash",
            AccountStatus.Active,
            now);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal("initial-hash", user.PasswordHash);
        Assert.Equal(now, user.EmailVerifiedAt);
        Assert.Equal(now, user.ActivatedAt);
        Assert.Equal(now, user.PasswordChangedAt);
        Assert.Equal("0912345678", user.PhoneNumber);
        Assert.Equal("GV001", user.UserCode);
    }

    [Fact]
    public void UpdateProfile_updates_fields_and_preserves_security_stamp()
    {
        var user = CreateActiveUser();
        var previousStamp = user.SecurityStamp;
        var now = CreatedAt.AddHours(2);

        user.UpdateProfile("Updated FullName", "0999999999", "NEWCODE", now);

        Assert.Equal("Updated FullName", user.FullName);
        Assert.Equal("0999999999", user.PhoneNumber);
        Assert.Equal("NEWCODE", user.UserCode);
        Assert.Equal(now, user.UpdatedAt);
        Assert.Equal(previousStamp, user.SecurityStamp);
    }

    [Fact]
    public void Reactivate_from_suspended_resets_lockout_and_sets_active()
    {
        var user = CreateActiveUser();
        var now = CreatedAt.AddHours(1);
        user.Suspend(now);
        Assert.Equal(AccountStatus.Suspended, user.Status);

        var previousStamp = user.SecurityStamp;
        var reactivateNow = now.AddHours(1);
        user.Reactivate(reactivateNow);

        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEndAt);
        Assert.NotEqual(previousStamp, user.SecurityStamp);
        Assert.Equal(reactivateNow, user.UpdatedAt);
    }

    [Fact]
    public void Reactivate_throws_when_account_is_pending_activation()
    {
        var user = CreatePendingUser();
        Assert.Throws<InvalidOperationException>(() => user.Reactivate(CreatedAt.AddMinutes(5)));
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

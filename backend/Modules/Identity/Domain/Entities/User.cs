using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Entities;

// Just rememeber one thing
// when I write code in this moment, only God and me know what I do
// After that only God know what I did

public sealed class User : AggregateRoot<Guid>
{
    private User() { }

    private User(Guid id) : base(id) { }

    private User(
        Guid id,
        string email,
        string normalizedEmail,
        string fullName,
        string userCode,
        string? phoneNumber,
        DateTime createdAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User identifier must be nonempty ", nameof(id));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userCode);
        EnsureUtc(createdAt, nameof(createdAt));

        Email = email.Trim();
        NormalizedEmail = normalizedEmail.Trim();
        FullName = fullName.Trim();
        UserCode = userCode.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber)
            ? null
            : phoneNumber.Trim();
        Status = AccountStatus.PendingActivation;
        SecurityStamp = Guid.NewGuid();
        ConcurrencyStamp = Guid.NewGuid();
        CreatedAt = createdAt;
    }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string? PasswordHash { get; private set; }

    public string FullName { get; private set; } = null!;
    public string? UserCode { get; private set; }
    public string? PhoneNumber { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }

    public int AccessFailedCount { get; private set; }
    public DateTime? LockoutEndAt { get; private set; }

    public DateTime? LastLoginAt { get; private set; }
    public DateTime? PasswordChangedAt { get; private set; }

    public Guid SecurityStamp { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = [];

    public static User ProvisionStudent(
        Guid id,
        string email,
        string normalizedEmail,
        string fullName,
        string userCode,
        string? phoneNumber,
        DateTime createdAt)
    {
        return new User(
            id,
            email,
            normalizedEmail,
            fullName,
            userCode,
            phoneNumber,
            createdAt);
    }

    public static User Create(
        Guid id,
        string email,
        string normalizedEmail,
        string fullName,
        string? userCode,
        string? phoneNumber,
        string? passwordHash,
        AccountStatus status,
        DateTime now)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User identifier must be nonempty", nameof(id));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        EnsureUtc(now, nameof(now));

        var user = new User(id)
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail.Trim(),
            FullName = fullName.Trim(),
            UserCode = string.IsNullOrWhiteSpace(userCode) ? null : userCode.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? null : passwordHash,
            Status = status,
            SecurityStamp = Guid.NewGuid(),
            ConcurrencyStamp = Guid.NewGuid(),
            CreatedAt = now
        };

        if (status == AccountStatus.Active && !string.IsNullOrWhiteSpace(passwordHash))
        {
            user.EmailVerifiedAt = now;
            user.ActivatedAt = now;
            user.PasswordChangedAt = now;
        }

        return user;
    }

    public bool IsLockedOut(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        return LockoutEndAt is not null && LockoutEndAt > now;
    }

    public void Activate(string passwordHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        EnsureUtc(now, nameof(now));

        if (Status != AccountStatus.PendingActivation)
        {
            throw new InvalidOperationException("Only a pending account can be activated");
        }
        PasswordHash = passwordHash;
        Status = AccountStatus.Active;
        EmailVerifiedAt = now;
        ActivatedAt = now;
        PasswordChangedAt = now;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        RotateSecurityStamp();
        Touch(now);
    }

    // lock account if login failed to much
    public bool RecordFailedLogin(
        DateTime now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration)
    {
        EnsureUtc(now, nameof(now));
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Login attempts can only be recorded for an active account");
        }

        if (IsLockedOut(now))
        {
            throw new InvalidOperationException("A failed login cannot be recorded while the account is locked");
        }

        if (maxFailedAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts));
        }

        if (lockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration));
        }

        AccessFailedCount++;
        var shouldLock = AccessFailedCount >= maxFailedAttempts;
        if (shouldLock)
        {
            LockoutEndAt = now.Add(lockoutDuration);
            AccessFailedCount = 0;
        }

        Touch(now);
        return shouldLock;
    }

    public void RecordSuccessfulLogin(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Only a active account can complete login");
        }

        if (IsLockedOut(now))
        {
            throw new InvalidOperationException("A locked account cannot complete login");
        }

        AccessFailedCount = 0;
        LockoutEndAt = null;
        LastLoginAt = now;
        Touch(now);
    }

    public void ChangePassword(string newPasswordHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        EnsureUtc(now, nameof(now));

        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Only a active account can change its password");
        }

        PasswordHash = newPasswordHash;
        PasswordChangedAt = now;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        RotateSecurityStamp();
        Touch(now);
    }

    public void Suspend(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (Status == AccountStatus.Suspended)
        {
            return;
        }

        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Only a active account can be suspended");
        }
        Status = AccountStatus.Suspended;
        RotateSecurityStamp();
        Touch(now);
    }

    public void Disable(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (Status == AccountStatus.Disabled)
        {
            return;
        }

        Status = AccountStatus.Disabled;
        RotateSecurityStamp();
        Touch(now);
    }

    public void UpdateProfile(string fullName, string? phoneNumber, string? userCode, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        EnsureUtc(now, nameof(now));

        FullName = fullName.Trim();
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        UserCode = string.IsNullOrWhiteSpace(userCode) ? null : userCode.Trim();
        Touch(now);
    }

    public void Reactivate(DateTime now)
    {
        EnsureUtc(now, nameof(now));
        if (Status == AccountStatus.Active)
        {
            return;
        }

        if (Status == AccountStatus.PendingActivation)
        {
            throw new InvalidOperationException("Pending activation accounts cannot be reactivated without completing activation.");
        }

        Status = AccountStatus.Active;
        AccessFailedCount = 0;
        LockoutEndAt = null;
        RotateSecurityStamp();
        Touch(now);
    }

    // Security Stamp show state security version of account
    public void RotateSecurityStamp()
    {
        SecurityStamp = Guid.NewGuid();
    }

    // 1. Update DateTime Editing
    // 2. Create a new version 
    private void Touch(DateTime now)
    {
        UpdatedAt = now;
        // -> Optimistic Concurrency Control - control conflict many request update 1 record
        ConcurrencyStamp = Guid.NewGuid();
    }

    // check a DateTime value is using Utc ?
    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time musbe UTC", parameterName);
        }
    }
}

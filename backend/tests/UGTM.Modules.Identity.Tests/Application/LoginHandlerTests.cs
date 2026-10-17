using UGTM.Modules.Identity.Application.Authentication.Login;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class LoginHandlerTests
{
    private static readonly DateTime Now =
        new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Pending_account_returns_pending_activation_error()
    {
        var user = CreatePendingUser();
        var result = await CreateHandler(user).HandleAsync(
            new LoginCommand(user.Email, "irrelevant", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AccountPendingActivation, result.Error);
    }

    [Fact]
    public async Task Suspended_account_returns_suspended_error_before_password_verification()
    {
        var user = CreatePendingUser();
        user.Activate("password-hash", Now.AddMinutes(1));
        user.Suspend(Now.AddMinutes(2));

        var result = await CreateHandler(user).HandleAsync(
            new LoginCommand(user.UserCode!, "irrelevant", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.AccountSuspended, result.Error);
    }

    private static LoginHandler CreateHandler(User user)
        => new(
            new StubUserRepository(user),
            null!,
            null!,
            null!,
            null!,
            null!,
            new InlineUnitOfWork(),
            null!,
            new IdentitySecurityPolicy());

    private static User CreatePendingUser()
        => User.ProvisionStudent(
            Guid.NewGuid(),
            "student@ugtm.edu.vn",
            "STUDENT@UGTM.EDU.VN",
            "Student",
            "SV001",
            null,
            Now);

    private sealed class InlineUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            => Task.FromResult(0);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
            => operation(cancellationToken);
    }

    private sealed class StubUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdentifierAsync(
            string normalizedEmail,
            string normalizedUserCode,
            CancellationToken cancellationToken)
            => Task.FromResult<User?>(user);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByUserCodeAsync(
            string userCode,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByEmailOrCodeAsync(
            string normalizedEmail,
            string userCode,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddToRoleAsync(
            User value,
            SystemRoleName role,
            DateTime assignedAt,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task UpdateRolesAsync(
            User value,
            IEnumerable<SystemRoleName> roles,
            DateTime assignedAt,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void Add(User value) => throw new NotSupportedException();
    }
}

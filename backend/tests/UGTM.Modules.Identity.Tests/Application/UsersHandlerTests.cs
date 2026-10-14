using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class UsersHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetUsersHandler_sanitizes_pagination_and_calls_query_reader()
    {
        var stubReader = new StubUserQueryReader();
        var handler = new GetUsersHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetUsersQuery(PageNumber: 0, PageSize: 500),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(stubReader.LastQuery);
        Assert.Equal(1, stubReader.LastQuery.PageNumber);
        Assert.Equal(100, stubReader.LastQuery.PageSize);
    }

    [Fact]
    public async Task CreateUserHandler_fails_when_email_or_fullname_empty()
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand("", "", null, null, ["Lecturer"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task CreateUserHandler_fails_when_role_is_invalid()
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand("test@ugtm.edu.vn", "Test Name", null, null, ["SuperAdminUnknown"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRole", result.Error.Code);
    }

    [Fact]
    public async Task CreateUserHandler_fails_when_email_exists()
    {
        var existingUser = User.Create(
            Guid.NewGuid(),
            "existing@ugtm.edu.vn",
            "EXISTING@UGTM.EDU.VN",
            "Existing User",
            "GV001",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([existingUser]);
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand("existing@ugtm.edu.vn", "New User", "GV002", null, ["Lecturer"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.DuplicateAccount.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateUserHandler_creates_active_user_when_initial_password_provided()
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand(
                "lecturer@ugtm.edu.vn",
                "TS. Nguyễn Văn B",
                "GV010",
                "0912345678",
                ["Lecturer", "CouncilMember"],
                InitialPassword: "ValidPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.Contains("Lecturer", result.Value.Roles);
        Assert.Contains("CouncilMember", result.Value.Roles);
        var created = Assert.Single(repo.Users);
        Assert.Equal(AccountStatus.Active, created.Status);
        Assert.NotNull(created.PasswordHash);
    }

    [Fact]
    public async Task CreateUserHandler_enqueues_activation_email_when_no_password()
    {
        var repo = new FakeUserRepository();
        var outbox = new FakeEmailOutbox();
        var handler = CreateHandler(repo, outbox: outbox);

        var result = await handler.HandleAsync(
            new CreateUserCommand(
                "newstudent@ugtm.edu.vn",
                "Sinh viên C",
                "SV010",
                null,
                ["Student"],
                InitialPassword: null,
                SendActivationEmail: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingActivation", result.Value.Status);
        Assert.True(outbox.Enqueued);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("03621201000")]
    [InlineData("abcdefghij")]
    public async Task CreateUserHandler_rejects_invalid_phone_number(string invalidPhone)
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand(
                "student@ugtm.edu.vn",
                "Student",
                "SV001",
                invalidPhone,
                ["Student"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPhoneNumber", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserHandler_rejects_invalid_phone_number()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Old Name",
            "OLD01",
            "0900000000",
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                user.Id,
                "New Name",
                "NEW01",
                "invalid-phone",
                ["Admin"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidPhoneNumber", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserHandler_updates_profile_and_roles()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Old Name",
            "OLD01",
            "0900000000",
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                user.Id,
                "New Name",
                "NEW01",
                "0911111111",
                ["Admin"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.FullName);
        Assert.Equal("NEW01", result.Value.UserCode);
        Assert.Equal("0911111111", result.Value.PhoneNumber);
        Assert.Contains("Admin", result.Value.Roles);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_suspends_and_reactivates_user()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test Name",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var suspendResult = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Suspended"),
            CancellationToken.None);
        Assert.True(suspendResult.IsSuccess);
        Assert.Equal("Suspended", suspendResult.Value.Status);

        var reactivateResult = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Active"),
            CancellationToken.None);
        Assert.True(reactivateResult.IsSuccess);
        Assert.Equal("Active", reactivateResult.Value.Status);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_fails_when_activating_pending_account()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test Name",
            "T01",
            null,
            null,
            AccountStatus.PendingActivation,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Active"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidStatusTransition", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_fails_when_suspending_non_active_account()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test Name",
            "T01",
            null,
            null,
            AccountStatus.Disabled,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Suspended"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidStatusTransition", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserHandler_preserves_security_stamp_when_roles_unchanged()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Old Name",
            "OLD01",
            "0900000000",
            null,
            AccountStatus.Active,
            Now);
        var oldStamp = user.SecurityStamp;
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Updated Name Only", null, "0911111111", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name Only", result.Value.FullName);
        Assert.Equal(oldStamp, user.SecurityStamp);
    }

    private static CreateUserHandler CreateHandler(
        FakeUserRepository repo,
        FakeEmailOutbox? outbox = null)
    {
        return new CreateUserHandler(
            repo,
            new FakeChallengeRepo(),
            outbox ?? new FakeEmailOutbox(),
            new FakeTokenService(),
            new FakePasswordPolicy(),
            new FakePasswordService(),
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new IdentitySecurityPolicy());
    }

    private sealed class StubUserQueryReader : IUserQueryReader
    {
        public GetUsersQuery? LastQuery { get; private set; }

        public Task<PagedResult<UserSummaryDto>> GetUsersAsync(
            GetUsersQuery query,
            CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<UserSummaryDto>([], 0, query.PageNumber, query.PageSize));
        }
    }

    private sealed class FakeUserRepository(IEnumerable<User>? initialUsers = null) : IUserRepository
    {
        public List<User> Users { get; } = [.. initialUsers ?? []];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail));

        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.UserCode == userCode));

        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.FirstOrDefault(u => u.NormalizedEmail == normalizedEmail || u.UserCode == normalizedUserCode));

        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail || u.UserCode == userCode));

        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.UserCode == userCode && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
            => Task.FromResult(Users.Any(u => u.NormalizedEmail == normalizedEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value)));

        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Add(User user) => Users.Add(user);
    }

    private sealed class FakeChallengeRepo : IOneTimeChallengeRepository
    {
        public Task<OneTimeChallenge?> GetAsync(string selector, ChallengePurpose purpose, CancellationToken cancellationToken)
            => Task.FromResult<OneTimeChallenge?>(null);

        public Task<OneTimeChallenge?> GetForUpdateAsync(string selector, ChallengePurpose purpose, CancellationToken cancellationToken)
            => Task.FromResult<OneTimeChallenge?>(null);

        public Task InvalidateOutstandingAsync(Guid userId, ChallengePurpose purpose, DateTime now, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Add(OneTimeChallenge challenge) { }
    }

    private sealed class FakeEmailOutbox : IIdentityEmailOutbox
    {
        public bool Enqueued { get; private set; }

        public void EnqueueActivationEmail(Guid userId, string to, string fullName, string rawToken, DateTime now)
        {
            Enqueued = true;
        }

        public void EnqueuePasswordRecoveryEmail(Guid userId, string email, string fullName, string code, DateTime createdAt) { }
    }

    private sealed class FakeTokenService : IOpaqueTokenService
    {
        public OpaqueToken CreateToken() => new("sel.val", "sel", [1, 2, 3]);
        public NumericCode CreateNumericCode(int digits) => new("sel", "123456", [1, 2, 3]);
        public bool TryParse(string rawToken, out string selector, out string validator) { selector = "sel"; validator = "val"; return true; }
        public bool Verify(string suppliedSecret, byte[] expectedHash) => true;
    }

    private sealed class FakePasswordPolicy : IPasswordPolicy
    {
        public PasswordPolicyResult Validate(string password) => new(password.Length >= 6, []);
    }

    private sealed class FakePasswordService : IPasswordService
    {
        public string Hash(User user, string password) => "hashed-" + password;
        public bool Verify(User user, string passwordHash, string providedPassword) => passwordHash == "hashed-" + providedPassword;
    }

    private sealed class DirectUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }
}

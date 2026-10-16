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

    [Theory]
    [InlineData("SuperAdminUnknown")]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task CreateUserHandler_fails_when_role_is_invalid(string invalidRole)
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand("test@ugtm.edu.vn", "Test Name", null, null, [invalidRole]),
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

    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("PendingActivation")]
    [InlineData("RandomStatus")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserStatusHandler_rejects_invalid_status_inputs(string invalidStatus)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, invalidStatus),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidStatus", result.Error.Code);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("UnknownRole")]
    public async Task UpdateUserHandler_rejects_invalid_roles(string invalidRole)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Updated Name", null, null, [invalidRole]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRole", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserHandler_rejects_empty_roles_list()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Updated Name", null, null, []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRole", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserHandler_rejects_empty_or_whitespace_full_name(string invalidName)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, invalidName, null, null, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidInput", result.Error.Code);
    }

    [Fact]
    public async Task CreateUserHandler_rejects_empty_or_null_roles()
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var nullRolesResult = await handler.HandleAsync(
            new CreateUserCommand("test@ugtm.edu.vn", "Test Name", null, null, null!),
            CancellationToken.None);
        Assert.False(nullRolesResult.IsSuccess);
        Assert.Equal("Identity.InvalidRole", nullRolesResult.Error.Code);

        var emptyRolesResult = await handler.HandleAsync(
            new CreateUserCommand("test@ugtm.edu.vn", "Test Name", null, null, []),
            CancellationToken.None);
        Assert.False(emptyRolesResult.IsSuccess);
        Assert.Equal("Identity.InvalidRole", emptyRolesResult.Error.Code);
    }

    [Fact]
    public async Task CreateUserHandler_deduplicates_duplicate_roles()
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand(
                "dedup@ugtm.edu.vn",
                "Dedup User",
                null,
                null,
                ["Lecturer", "Lecturer", "lecturer"],
                InitialPassword: "ValidPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Roles);
        Assert.Equal("Lecturer", result.Value.Roles[0]);
    }

    [Fact]
    public async Task GetUsersHandler_sanitizes_pagination_bounds()
    {
        var stubReader = new StubUserQueryReader();
        var handler = new GetUsersHandler(stubReader);

        await handler.HandleAsync(
            new GetUsersQuery(null, null, null, PageNumber: -10, PageSize: -5),
            CancellationToken.None);
        Assert.NotNull(stubReader.LastQuery);
        Assert.Equal(1, stubReader.LastQuery.PageNumber);
        Assert.Equal(10, stubReader.LastQuery.PageSize);

        await handler.HandleAsync(
            new GetUsersQuery(null, null, null, PageNumber: 2, PageSize: 500),
            CancellationToken.None);
        Assert.NotNull(stubReader.LastQuery);
        Assert.Equal(2, stubReader.LastQuery.PageNumber);
        Assert.Equal(100, stubReader.LastQuery.PageSize);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("Unknown")]
    public async Task GetUsersHandler_rejects_invalid_role_filter(string invalidRole)
    {
        var stubReader = new StubUserQueryReader();
        var handler = new GetUsersHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetUsersQuery(null, Role: invalidRole, Status: null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidRole", result.Error.Code);
        Assert.Null(stubReader.LastQuery);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("InvalidStatus")]
    public async Task GetUsersHandler_rejects_invalid_status_filter(string invalidStatus)
    {
        var stubReader = new StubUserQueryReader();
        var handler = new GetUsersHandler(stubReader);

        var result = await handler.HandleAsync(
            new GetUsersQuery(null, Role: null, Status: invalidStatus),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidStatus", result.Error.Code);
        Assert.Null(stubReader.LastQuery);
    }

    [Fact]
    public async Task CreateUserHandler_and_UpdateUserHandler_reject_null_or_whitespace_role_elements()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var createHandler = CreateHandler(repo);
        var updateHandler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var createNullElem = await createHandler.HandleAsync(
            new CreateUserCommand("new@ugtm.edu.vn", "New User", null, null, [null!]),
            CancellationToken.None);
        Assert.False(createNullElem.IsSuccess);
        Assert.Equal("Identity.InvalidRole", createNullElem.Error.Code);

        var updateNullElem = await updateHandler.HandleAsync(
            new UpdateUserCommand(user.Id, "Test User", null, null, ["Lecturer", null!]),
            CancellationToken.None);
        Assert.False(updateNullElem.IsSuccess);
        Assert.Equal("Identity.InvalidRole", updateNullElem.Error.Code);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at.com")]
    [InlineData("@missing-local.com")]
    [InlineData("missing-domain@")]
    [InlineData("missing-tld@domain")]
    public async Task CreateUserHandler_rejects_invalid_email_format(string invalidEmail)
    {
        var repo = new FakeUserRepository();
        var handler = CreateHandler(repo);

        var result = await handler.HandleAsync(
            new CreateUserCommand(invalidEmail, "Valid Name", "SV01", null, ["Student"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidInput", result.Error.Code);
    }

    [Fact]
    public async Task CreateUserHandler_and_UpdateUserHandler_reject_fields_exceeding_max_length()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Active,
            Now);
        var repo = new FakeUserRepository([user]);
        var createHandler = CreateHandler(repo);
        var updateHandler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var longEmail = new string('a', 142) + "@uit.edu.vn"; // 153 chars > 150
        var longName = new string('N', 151); // > 150
        var longCode = new string('C', 21); // > 20

        var resEmail = await createHandler.HandleAsync(
            new CreateUserCommand(longEmail, "Valid Name", "SV01", null, ["Student"]),
            CancellationToken.None);
        Assert.False(resEmail.IsSuccess);
        Assert.Equal("Identity.InvalidInput", resEmail.Error.Code);

        var resCreateName = await createHandler.HandleAsync(
            new CreateUserCommand("valid@ugtm.edu.vn", longName, "SV01", null, ["Student"]),
            CancellationToken.None);
        Assert.False(resCreateName.IsSuccess);
        Assert.Equal("Identity.InvalidInput", resCreateName.Error.Code);

        var resCreateCode = await createHandler.HandleAsync(
            new CreateUserCommand("valid@ugtm.edu.vn", "Valid Name", longCode, null, ["Student"]),
            CancellationToken.None);
        Assert.False(resCreateCode.IsSuccess);
        Assert.Equal("Identity.InvalidInput", resCreateCode.Error.Code);

        var resUpdateName = await updateHandler.HandleAsync(
            new UpdateUserCommand(user.Id, longName, "T01", null, null),
            CancellationToken.None);
        Assert.False(resUpdateName.IsSuccess);
        Assert.Equal("Identity.InvalidInput", resUpdateName.Error.Code);

        var resUpdateCode = await updateHandler.HandleAsync(
            new UpdateUserCommand(user.Id, "Valid Name", longCode, null, null),
            CancellationToken.None);
        Assert.False(resUpdateCode.IsSuccess);
        Assert.Equal("Identity.InvalidInput", resUpdateCode.Error.Code);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_is_idempotent_and_preserves_security_stamp_when_status_unchanged()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@ugtm.edu.vn",
            "TEST@UGTM.EDU.VN",
            "Test User",
            "T01",
            null,
            null,
            AccountStatus.Suspended,
            Now);
        var oldStamp = user.SecurityStamp;
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Suspended"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Suspended", result.Value.Status);
        Assert.Equal(oldStamp, user.SecurityStamp);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Faculty")]
    [InlineData("AcademicAffairs")]
    [InlineData("Lecturer")]
    [InlineData("CouncilMember")]
    public async Task CreateUserHandler_rejects_student_combined_with_other_roles(string otherRole)
    {
        var repo = new FakeUserRepository();
        var result = await CreateHandler(repo).HandleAsync(
            new CreateUserCommand("student@uit.edu.vn", "Student", null, null, [" student ", otherRole]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.StudentRoleConflict.Code, result.Error.Code);
        Assert.Empty(repo.Users);
    }

    [Theory]
    [InlineData(SystemRoleName.Admin)]
    [InlineData(SystemRoleName.Faculty)]
    [InlineData(SystemRoleName.AcademicAffairs)]
    [InlineData(SystemRoleName.Lecturer)]
    [InlineData(SystemRoleName.CouncilMember)]
    public async Task UpdateUserHandler_rejects_replacing_non_student_role_with_student(SystemRoleName existingRole)
    {
        var user = CreateUserWithRole(existingRole);
        var oldStamp = user.SecurityStamp;
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Changed Name", null, null, ["Student"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.CannotBecomeStudent.Code, result.Error.Code);
        Assert.Equal("Original Name", user.FullName);
        Assert.Equal(oldStamp, user.SecurityStamp);
        Assert.Equal(existingRole, Assert.Single(user.UserRoles).Role.RoleName);
    }

    [Fact]
    public async Task UpdateUserHandler_rejects_adding_other_role_to_student()
    {
        var user = CreateUserWithRole(SystemRoleName.Student);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Changed Name", null, null, ["Student", "Lecturer"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IdentityErrors.StudentRoleConflict.Code, result.Error.Code);
        Assert.Equal("Original Name", user.FullName);
    }

    [Theory]
    [InlineData(SystemRoleName.Student, "Student", "student")]
    [InlineData(SystemRoleName.Lecturer, "Lecturer", "CouncilMember")]
    public async Task UpdateUserHandler_allows_valid_role_combinations(
        SystemRoleName existingRole, string firstRole, string secondRole)
    {
        var user = CreateUserWithRole(existingRole);
        var repo = new FakeUserRepository([user]);
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now));

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Changed Name", null, null, [firstRole, secondRole]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Changed Name", user.FullName);
        Assert.Equal(firstRole == "Student" ? 1 : 2, result.Value.Roles.Count);
    }

    private static User CreateUserWithRole(SystemRoleName role)
    {
        var user = User.Create(Guid.NewGuid(), "test@uit.edu.vn", "TEST@UIT.EDU.VN",
            "Original Name", null, null, null, AccountStatus.Active, Now);
        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = (int)role + 1,
            Role = new Role { Id = (int)role + 1, RoleName = role },
            AssignedAt = Now,
            User = user
        });
        return user;
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

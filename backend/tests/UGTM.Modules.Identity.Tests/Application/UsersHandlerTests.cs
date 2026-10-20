using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Application.Users.CreateUser;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Application.Users.ImportUsers;
using UGTM.Modules.Identity.Application.Users.UpdateUser;
using UGTM.Modules.Identity.Application.Users.UpdateUserStatus;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class UsersHandlerTests
{
    [Theory]
    [InlineData("student", "Student")]
    [InlineData(" STAFF ", "Staff")]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    public async Task GetUsersHandler_normalizes_account_groups(string? input, string? expected)
    {
        var reader = new StubUserQueryReader();
        var result = await new GetUsersHandler(reader).HandleAsync(
            new GetUsersQuery(AccountGroup: input), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, reader.LastQuery!.AccountGroup);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("Lecturer")]
    [InlineData("Student, Staff")]
    public async Task GetUsersHandler_rejects_invalid_account_groups(string input)
    {
        var reader = new StubUserQueryReader();
        var result = await new GetUsersHandler(reader).HandleAsync(
            new GetUsersQuery(AccountGroup: input), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidAccountGroup", result.Error.Code);
        Assert.Null(reader.LastQuery);
    }

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
    public async Task ImportUsersHandler_imports_valid_students_as_pending_without_sending_email()
    {
        var repo = new FakeUserRepository();
        var auditLogs = new FakeAuditLogWriter();
        var handler = CreateImportHandler(
            repo,
            new ImportFileReadResult(
                [
                    new ImportUserRow(2, "student1@ugtm.edu.vn", "Student One", "sv001", "0901234567"),
                    new ImportUserRow(3, "student2@ugtm.edu.vn", "Student Two", "sv002", null)
                ],
                []),
            auditLogs);

        var result = await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "students.xlsx", "Student"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Errors);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.All(repo.Users, user =>
        {
            Assert.Equal(AccountStatus.PendingActivation, user.Status);
            Assert.Null(user.PasswordHash);
            Assert.Contains(user.UserRoles, userRole => userRole.Role.RoleName == SystemRoleName.Student);
        });
        Assert.Contains(repo.Users, user => user.UserCode == "SV001");
        Assert.Equal(AuditActions.UsersImported, Assert.Single(auditLogs.Entries).Action);
    }

    [Fact]
    public async Task ImportUsersHandler_rejects_duplicate_rows_without_importing_any_user()
    {
        var repo = new FakeUserRepository();
        var handler = CreateImportHandler(
            repo,
            new ImportFileReadResult(
                [
                    new ImportUserRow(2, "duplicate@ugtm.edu.vn", "Student One", "SV001", null),
                    new ImportUserRow(3, "DUPLICATE@ugtm.edu.vn", "Student Two", "SV002", null)
                ],
                []));

        var result = await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "students.xlsx", "Student"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ImportedCount);
        Assert.Equal(2, result.Value.Errors.Count(error => error.Code == "DuplicateEmailInFile"));
        Assert.Empty(repo.Users);
    }

    [Fact]
    public async Task ImportUsersHandler_rejects_database_conflict_without_importing_valid_rows()
    {
        var existing = User.Create(
            Guid.NewGuid(),
            "existing@ugtm.edu.vn",
            "EXISTING@UGTM.EDU.VN",
            "Existing",
            "GV001",
            null,
            null,
            AccountStatus.PendingActivation,
            Now);
        var repo = new FakeUserRepository([existing]);
        var handler = CreateImportHandler(
            repo,
            new ImportFileReadResult(
                [
                    new ImportUserRow(2, "existing@ugtm.edu.vn", "Existing Again", "GV002", null),
                    new ImportUserRow(3, "new@ugtm.edu.vn", "New Lecturer", "GV003", null)
                ],
                []));

        var result = await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "lecturers.xlsx", "Lecturer"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ImportedCount);
        Assert.Contains(result.Value.Errors, error => error.RowNumber == 2 && error.Code == "DuplicateEmail");
        Assert.Single(repo.Users);
    }

    [Fact]
    public async Task ImportUsersHandler_allows_lecturer_without_user_code()
    {
        var repo = new FakeUserRepository();
        var handler = CreateImportHandler(
            repo,
            new ImportFileReadResult(
                [new ImportUserRow(2, "lecturer@ugtm.edu.vn", "Lecturer", null, null)],
                []));

        var result = await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "lecturers.xlsx", "Lecturer"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ImportedCount);
        Assert.Null(Assert.Single(repo.Users).UserCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Faculty")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("unknown")]
    public async Task ImportUsersHandler_rejects_unsupported_role(string role)
    {
        var handler = CreateImportHandler(
            new FakeUserRepository(),
            new ImportFileReadResult([], []));

        var result = await handler.HandleAsync(
            new ImportUsersCommand(new MemoryStream([1]), "users.xlsx", role),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.InvalidImportRole", result.Error.Code);
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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var updateHandler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var updateHandler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserStatusHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

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
        var handler = new UpdateUserHandler(repo, new DirectUnitOfWork(), new FakeClock(Now), new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserCommand(user.Id, "Changed Name", null, null, [firstRole, secondRole]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Changed Name", user.FullName);
        Assert.Equal(firstRole == "Student" ? 1 : 2, result.Value.Roles.Count);
    }

    private static User CreateUserWithRole(SystemRoleName role, AccountStatus status = AccountStatus.Active)
    {
        var user = User.Create(Guid.NewGuid(), "test@uit.edu.vn", "TEST@UIT.EDU.VN",
            "Original Name", null, null, null, status, Now);
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

    [Fact]
    public async Task CreateUserHandler_writes_audit_entry_without_sensitive_password_data()
    {
        var repo = new FakeUserRepository();
        var auditLogs = new FakeAuditLogWriter();
        var handler = CreateHandler(repo, auditLogs: auditLogs);

        var result = await handler.HandleAsync(
            new CreateUserCommand(
                "new.user@ugtm.edu.vn",
                "New User",
                "USR001",
                null,
                ["Lecturer"],
                "Secret123!",
                SendActivationEmail: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.UserCreated, entry.Action);
        Assert.Equal(result.Value.Id.ToString("D"), entry.TargetId);
        Assert.DoesNotContain("Secret123!", System.Text.Json.JsonSerializer.Serialize(entry.Metadata));
    }

    [Fact]
    public async Task UpdateUserHandler_writes_only_the_audits_for_actual_changes()
    {
        var user = CreateUserWithRole(SystemRoleName.Lecturer);
        var repo = new FakeUserRepository([user]);
        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateUserHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            auditLogs);

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                user.Id,
                "Changed Name",
                "GV001",
                "0901234567",
                ["Faculty"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, auditLogs.Entries.Count);
        Assert.Contains(auditLogs.Entries, entry => entry.Action == AuditActions.UserUpdated);
        Assert.Contains(auditLogs.Entries, entry => entry.Action == AuditActions.UserRolesChanged);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_does_not_audit_an_idempotent_request()
    {
        var user = CreateUserWithRole(SystemRoleName.Lecturer);
        var repo = new FakeUserRepository([user]);
        var auditLogs = new FakeAuditLogWriter();
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            auditLogs);

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(user.Id, "Active"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(auditLogs.Entries);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_fails_when_disabling_last_admin()
    {
        var admin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin]);
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(admin.Id, "Disabled"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.CannotDisableLastAdmin", result.Error.Code);
        Assert.Equal(1, repo.AdminMutationLockCount);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_fails_when_suspending_last_active_admin()
    {
        var admin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin]);
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(admin.Id, "Suspended"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.CannotDisableLastAdmin", result.Error.Code);
        Assert.Equal(1, repo.AdminMutationLockCount);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_allows_disabling_admin_when_another_active_admin_exists()
    {
        var admin1 = CreateUserWithRole(SystemRoleName.Admin);
        var admin2 = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin1, admin2]);
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(admin1.Id, "Disabled"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Disabled", result.Value.Status);
    }

    [Theory]
    [InlineData(AccountStatus.Disabled)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.PendingActivation)]
    public async Task UpdateUserStatusHandler_fails_when_disabling_last_active_admin_when_other_admin_is_not_active(AccountStatus otherAdminStatus)
    {
        var activeAdmin = CreateUserWithRole(SystemRoleName.Admin, AccountStatus.Active);
        var nonActiveAdmin = CreateUserWithRole(SystemRoleName.Admin, otherAdminStatus);
        var repo = new FakeUserRepository([activeAdmin, nonActiveAdmin]);
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(activeAdmin.Id, "Disabled"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.CannotDisableLastAdmin", result.Error.Code);
    }

    [Fact]
    public async Task UpdateUserStatusHandler_allows_disabling_non_admin_user()
    {
        var lecturer = CreateUserWithRole(SystemRoleName.Lecturer);
        var repo = new FakeUserRepository([lecturer]);
        var handler = new UpdateUserStatusHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserStatusCommand(lecturer.Id, "Disabled"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Disabled", result.Value.Status);
    }

    [Fact]
    public async Task UpdateUserHandler_fails_when_removing_admin_role_from_last_admin()
    {
        var admin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin]);
        var handler = new UpdateUserHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                admin.Id,
                "Updated Admin",
                admin.UserCode,
                null,
                ["Lecturer"]),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Identity.CannotRemoveLastAdminRole", result.Error.Code);
        Assert.Equal(1, repo.AdminMutationLockCount);
    }

    [Fact]
    public async Task UpdateUserHandler_allows_removing_admin_role_when_another_active_admin_exists()
    {
        var admin1 = CreateUserWithRole(SystemRoleName.Admin);
        var admin2 = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin1, admin2]);
        var handler = new UpdateUserHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                admin1.Id,
                "Demoted Admin",
                admin1.UserCode,
                null,
                ["Lecturer"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("Lecturer", result.Value.Roles);
        Assert.DoesNotContain("Admin", result.Value.Roles);
    }

    [Fact]
    public async Task UpdateUserHandler_allows_admin_to_update_profile_and_keep_admin_role()
    {
        var admin = CreateUserWithRole(SystemRoleName.Admin);
        var repo = new FakeUserRepository([admin]);
        var handler = new UpdateUserHandler(
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            new FakeAuditLogWriter());

        var result = await handler.HandleAsync(
            new UpdateUserCommand(
                admin.Id,
                "Updated Admin Name",
                admin.UserCode,
                "0987654321",
                ["Admin", "Faculty"]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Admin Name", result.Value.FullName);
        Assert.Contains("Admin", result.Value.Roles);
        Assert.Contains("Faculty", result.Value.Roles);
    }

    private static CreateUserHandler CreateHandler(
        FakeUserRepository repo,
        FakeEmailOutbox? outbox = null,
        FakeAuditLogWriter? auditLogs = null)
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
            new IdentitySecurityPolicy(),
            auditLogs ?? new FakeAuditLogWriter());
    }

    private static ImportUsersHandler CreateImportHandler(
        FakeUserRepository repo,
        ImportFileReadResult readResult,
        FakeAuditLogWriter? auditLogs = null)
        => new(
            new StubUserImportFileReader(readResult),
            repo,
            new DirectUnitOfWork(),
            new FakeClock(Now),
            auditLogs ?? new FakeAuditLogWriter());

    private sealed class StubUserImportFileReader(ImportFileReadResult result) : IUserImportFileReader
    {
        public ImportFileReadResult Read(Stream stream) => result;
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
        public int AdminMutationLockCount { get; private set; }

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

        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users
                .Where(user => normalizedEmails.Contains(user.NormalizedEmail))
                .Select(user => user.NormalizedEmail)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlySet<string>>(Users
                .Where(user => user.UserCode is not null && userCodes.Contains(user.UserCode))
                .Select(user => user.UserCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        public Task AddToRoleAsync(User user, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = (int)role + 1,
                Role = new Role { Id = (int)role + 1, RoleName = role },
                AssignedAt = assignedAt,
                User = user
            });
            return Task.CompletedTask;
        }

        public async Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken)
        {
            foreach (var user in users)
            {
                Users.Add(user);
                await AddToRoleAsync(user, role, assignedAt, cancellationToken);
            }
        }

        public Task UpdateRolesAsync(User user, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken)
        {
            user.UserRoles.Clear();
            foreach (var role in roles)
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = (int)role + 1,
                    Role = new Role { Id = (int)role + 1, RoleName = role },
                    AssignedAt = assignedAt,
                    User = user
                });
            }
            return Task.CompletedTask;
        }

        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken)
        {
            var count = Users
                .Where(u => u.Status == AccountStatus.Active && u.UserRoles.Any(ur => ur.Role.RoleName == SystemRoleName.Admin))
                .Where(u => !excludeUserId.HasValue || u.Id != excludeUserId.Value)
                .Count();
            return Task.FromResult(count);
        }

        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken)
        {
            AdminMutationLockCount++;
            return Task.CompletedTask;
        }

        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken)
        {
            var exists = Users.Any(u => u.UserRoles.Any(ur => ur.Role.RoleName == SystemRoleName.Admin));
            return Task.FromResult(exists);
        }

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

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public void Add(AuditEntry entry) => Entries.Add(entry);
    }
}

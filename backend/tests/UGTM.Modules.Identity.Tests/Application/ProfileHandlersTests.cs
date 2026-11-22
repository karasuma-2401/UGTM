using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Profiles;
using UGTM.Modules.Identity.Application.Profiles.GetMyActivity;
using UGTM.Modules.Identity.Application.Profiles.UpdateMyProfile;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Tests.Application;

public sealed class ProfileHandlersTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateMyProfile_UpdatesPhoneNumber_AndWritesAuditLog()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "24521186@gm.uit.edu.vn",
            "24521186@GM.UIT.EDU.VN",
            "Nguyễn Văn A",
            "24521186",
            "0901111222",
            "hash",
            AccountStatus.Active,
            Now.AddDays(-10));
        var currentUser = new FakeCurrentUser(user.Id, true);
        var userRepo = new FakeUserRepository(user);
        var profileReader = new FakeUserProfileReader(user);
        var unitOfWork = new FakeUnitOfWork();
        var auditLogs = new FakeAuditLogWriter();
        var clock = new FakeClock(Now);

        var handler = new UpdateMyProfileHandler(
            currentUser,
            userRepo,
            profileReader,
            unitOfWork,
            auditLogs,
            clock);

        var result = await handler.HandleAsync(new UpdateMyProfileCommand(" 0987654321 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("0987654321", user.PhoneNumber);
        Assert.Equal("0987654321", result.Value.PhoneNumber);
        Assert.Equal("Nguyễn Văn A", user.FullName);
        Assert.Equal("24521186", user.UserCode);
        Assert.Single(auditLogs.Entries);
        Assert.Equal(AuditActions.ProfileUpdated, auditLogs.Entries[0].Action);
        Assert.Equal(AuditOutcomes.Success, auditLogs.Entries[0].Outcome);
    }

    [Fact]
    public async Task UpdateMyProfile_RejectsInvalidPhoneNumber()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "24521186@gm.uit.edu.vn",
            "24521186@GM.UIT.EDU.VN",
            "Nguyễn Văn A",
            "24521186",
            null,
            "hash",
            AccountStatus.Active,
            Now.AddDays(-10));
        var handler = new UpdateMyProfileHandler(
            new FakeCurrentUser(user.Id, true),
            new FakeUserRepository(user),
            new FakeUserProfileReader(user),
            new FakeUnitOfWork(),
            new FakeAuditLogWriter(),
            new FakeClock(Now));

        var result = await handler.HandleAsync(new UpdateMyProfileCommand("12345"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityErrors.InvalidPhoneNumber, result.Error);
    }

    [Fact]
    public async Task GetMyActivity_ClampsPageSize_AndScopesToCurrentUser()
    {
        var userId = Guid.NewGuid();
        var reader = new FakeAuditLogQueryReader();
        var handler = new GetMyActivityHandler(new FakeCurrentUser(userId, true), reader);

        var result = await handler.HandleAsync(0, 999, "auth", "2026-11-22", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.CapturedQuery);
        Assert.Equal(userId, reader.CapturedQuery!.ActorUserId);
        Assert.Equal(1, reader.CapturedQuery.PageNumber);
        Assert.Equal(30, reader.CapturedQuery.PageSize);
        Assert.Equal("timestamp", reader.CapturedQuery.SortBy);
        Assert.Equal("desc", reader.CapturedQuery.SortOrder);
        Assert.NotNull(reader.CapturedQuery.Actions);
        Assert.Contains(AuditActions.Login, reader.CapturedQuery.Actions!);
        Assert.Equal(new DateTime(2026, 11, 21, 17, 0, 0, DateTimeKind.Utc), reader.CapturedQuery.FromUtc);
        Assert.Equal(new DateTime(2026, 11, 22, 17, 0, 0, DateTimeKind.Utc), reader.CapturedQuery.ToUtc);
    }

    [Fact]
    public async Task GetMyActivitySummary_ClampsDays_AndComputesVietnamWindowStart()
    {
        var userId = Guid.NewGuid();
        var reader = new FakeAuditLogQueryReader();
        var handler = new GetMyActivitySummaryHandler(
            new FakeCurrentUser(userId, true),
            reader,
            new FakeClock(Now));

        var result = await handler.HandleAsync(112, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, reader.CapturedSummaryUserId);
        Assert.Equal(TimeSpan.FromHours(7), reader.CapturedSummaryOffset);
        Assert.Equal(Now, reader.CapturedSummaryUtcNow);
    }

    private sealed class FakeCurrentUser(Guid? userId, bool isAuthenticated) : ICurrentUser
    {
        public bool IsAuthenticated => isAuthenticated;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(id == user.Id ? user : null);
        public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByUserCodeAsync(string userCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByIdentifierAsync(string normalizedEmail, string normalizedUserCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailOrCodeAsync(string normalizedEmail, string userCode, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByUserCodeAsync(string userCode, Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsByEmailAsync(string normalizedEmail, Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingNormalizedEmailsAsync(IReadOnlyCollection<string> normalizedEmails, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> GetExistingUserCodesAsync(IReadOnlyCollection<string> userCodes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddToRoleAsync(User value, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddRangeToRoleAsync(IReadOnlyCollection<User> users, SystemRoleName role, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateRolesAsync(User value, IEnumerable<SystemRoleName> roles, DateTime assignedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AcquireAccountProvisioningLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AcquireAdminMutationLockAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> CountActiveAdminsAsync(Guid? excludeUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AnyAdminExistsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(User value) => throw new NotSupportedException();
    }

    private sealed class FakeUserProfileReader(User user) : IUserProfileReader
    {
        public Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<UserProfileDto?>(
                userId == user.Id
                    ? new UserProfileDto(
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.UserCode,
                        ["Student"],
                        [],
                        user.MustChangePassword,
                        user.PhoneNumber,
                        user.Status.ToString(),
                        user.CreatedAt,
                        user.ActivatedAt,
                        user.LastLoginAt,
                        user.PasswordChangedAt,
                        [])
                    : null);
    }

    private sealed class FakeUnitOfWork : IIdentityUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    private sealed class FakeAuditLogWriter : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Add(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class FakeAuditLogQueryReader : IAuditLogQueryReader
    {
        public GetAuditLogsQuery? CapturedQuery { get; private set; }
        public Guid? CapturedSummaryUserId { get; private set; }
        public TimeSpan? CapturedSummaryOffset { get; private set; }
        public DateTime? CapturedSummaryUtcNow { get; private set; }

        public Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
            GetAuditLogsQuery query,
            CancellationToken cancellationToken)
        {
            CapturedQuery = query;
            return Task.FromResult(new PagedResult<AuditLogDto>([], 0, query.PageNumber, query.PageSize));
        }

        public Task<UserActivitySummaryDto> GetUserActivitySummaryAsync(
            Guid userId,
            DateTime fromUtc,
            TimeSpan utcOffset,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            CapturedSummaryUserId = userId;
            CapturedSummaryOffset = utcOffset;
            CapturedSummaryUtcNow = utcNow;
            return Task.FromResult(new UserActivitySummaryDto([], 0, 0, 0));
        }

        public Task<AuditLogStatistics> GetStatisticsAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public Task<List<string>> GetActionsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
        public IAsyncEnumerable<AuditLogDto> ExportAsync(GetAuditLogsQuery query, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}

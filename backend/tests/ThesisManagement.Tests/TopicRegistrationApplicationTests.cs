using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.ConfirmTopicRegistration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.GetMyRegistrationStatus;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.RegisterTopic;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.BackgroundServices;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class TopicRegistrationApplicationTests
{
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid TopicId1 = Guid.NewGuid();
    private static readonly Guid TopicId2 = Guid.NewGuid();
    private static readonly Guid StudentAId = Guid.NewGuid();
    private static readonly Guid StudentBId = Guid.NewGuid();
    private static readonly Guid StudentCId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeTopicRegistrationStore _store = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);

    public TopicRegistrationApplicationTests()
    {
        _store.SeedTopic(TopicId1, BatchId, studentNumber: 2, status: "Published");
        _store.SeedTopic(TopicId2, BatchId, studentNumber: 1, status: "Published");

        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentAId, "studentA@test.edu.vn", "Nguyen Van A", "SV001", null));
        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentBId, "studentB@test.edu.vn", "Tran Thi B", "SV002", null));
        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentCId, "studentC@test.edu.vn", "Le Van C", "SV003", null));
    }

    [Fact]
    public async Task Register_SoloStudent_Succeeds_AndIsConfirmedImmediately()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPendingConfirmation);
        Assert.Equal(TopicId1, result.Value.TopicId);

        var status = await _store.GetStudentRegistrationStatusAsync(StudentAId);
        Assert.NotNull(status);
        Assert.True(status.HasRegistered);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), status.ConfirmationStatus);
    }

    [Fact]
    public async Task Register_TwoStudents_Succeeds_StudentAConfirmed_StudentBPending_EmailsSent()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPendingConfirmation);
        Assert.NotNull(result.Value.ConfirmationExpiresAtUtc);

        var statusA = await _store.GetStudentRegistrationStatusAsync(StudentAId);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), statusA!.ConfirmationStatus);

        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.Equal(nameof(MemberConfirmationStatus.Pending), statusB!.ConfirmationStatus);

        // Verify emails were sent to both
        Assert.Equal(2, _emailSender.SentMessages.Count);
        Assert.Contains(_emailSender.SentMessages, m => m.To == "studentB@test.edu.vn");
        Assert.Contains(_emailSender.SentMessages, m => m.To == "studentA@test.edu.vn");
    }

    [Fact]
    public async Task Register_TwoSoloStudents_MergeIntoSameGroup()
    {
        var handlerA = CreateRegisterHandler(StudentAId);
        var resultA = await handlerA.HandleAsync(new RegisterTopicCommand(TopicId1));
        Assert.True(resultA.IsSuccess);

        var handlerB = CreateRegisterHandler(StudentBId);
        var resultB = await handlerB.HandleAsync(new RegisterTopicCommand(TopicId1));
        Assert.True(resultB.IsSuccess);

        // Same group ID!
        Assert.Equal(resultA.Value.GroupId, resultB.Value.GroupId);

        var members = _store.GetGroupMembers(resultA.Value.GroupId);
        Assert.Equal(2, members.Count);
    }

    [Fact]
    public async Task Register_ExceedsQuota_ReturnsTopicAlreadyTaken()
    {
        var handlerA = CreateRegisterHandler(StudentAId);
        await handlerA.HandleAsync(new RegisterTopicCommand(TopicId1));

        var handlerB = CreateRegisterHandler(StudentBId);
        await handlerB.HandleAsync(new RegisterTopicCommand(TopicId1));

        // Topic 1 now has 2/2 slots filled. Student C tries to register:
        var handlerC = CreateRegisterHandler(StudentCId);
        var resultC = await handlerC.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(resultC.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicAlreadyTaken.Code, resultC.Error.Code);
    }

    [Fact]
    public async Task Register_WhenWindowClosed_ReturnsRegistrationWindowClosed()
    {
        _store.IsRegistrationWindowOpen = false;

        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.RegistrationWindowClosed.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_WhenTopicNotPublished_ReturnsTopicNotPublished()
    {
        _store.SeedTopic(TopicId1, BatchId, studentNumber: 2, status: "Draft");

        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicNotPublished.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_StudentAlreadyRegisteredInBatch_ReturnsError()
    {
        var handlerA = CreateRegisterHandler(StudentAId);
        await handlerA.HandleAsync(new RegisterTopicCommand(TopicId1));

        // Student A tries to register another topic in the same batch:
        var result2 = await handlerA.HandleAsync(new RegisterTopicCommand(TopicId2));

        Assert.False(result2.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch.Code, result2.Error.Code);
    }

    [Fact]
    public async Task Register_StudentRegistrationBlocked_ReturnsStudentRegistrationBlocked()
    {
        _store.SetPenalty(StudentAId, violationCount: 3, isBlocked: true);

        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentRegistrationBlocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_PartnerRegistrationBlocked_ReturnsPartnerRegistrationBlocked()
    {
        _store.SetPenalty(StudentBId, violationCount: 3, isBlocked: true);

        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.PartnerRegistrationBlocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_GroupSizeExceedsQuota_ReturnsGroupSizeExceedsTopicQuota()
    {
        // Topic 2 has StudentNumber = 1. Trying to register with partner (size = 2):
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId2, PartnerStudentId: StudentBId));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.GroupSizeExceedsTopicQuota.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_InvitingSelf_ReturnsCannotInviteSelf()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentAId));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.CannotInviteSelf.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_PartnerNotFound_ReturnsPartnerNotFound()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.PartnerNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Confirm_ValidToken_ConfirmsMember()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        // Get the token generated for Student B
        var token = _store.GetLastGeneratedRawToken();
        Assert.NotNull(token);

        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var confirmResult = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand(token));
        Assert.True(confirmResult.IsSuccess);

        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), statusB!.ConfirmationStatus);
    }

    [Fact]
    public async Task Confirm_InvalidToken_ReturnsInvalidConfirmationToken()
    {
        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var confirmResult = await confirmHandler.HandleAsync(
            new ConfirmTopicRegistrationCommand("invalid-token-123"));

        Assert.False(confirmResult.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.InvalidConfirmationToken.Code, confirmResult.Error.Code);
    }

    [Fact]
    public async Task Confirm_ExpiredToken_ReturnsConfirmationExpired()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        var token = _store.GetLastGeneratedRawToken();

        // Advance time past 24 hours
        _timeProvider.Advance(TimeSpan.FromHours(25));

        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var confirmResult = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand(token!));

        Assert.False(confirmResult.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.ConfirmationExpired.Code, confirmResult.Error.Code);
    }

    [Fact]
    public async Task Enforcer_CleansUpExpiredPendingMembers_AndWarnsStudent()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        _timeProvider.Advance(TimeSpan.FromHours(25));
        _emailSender.SentMessages.Clear();

        var enforcer = new TopicRegistrationExpirationEnforcer(
            _store,
            _studentInfoReader,
            _emailSender,
            _timeProvider,
            NullLogger<TopicRegistrationExpirationEnforcer>.Instance);

        var result = await enforcer.EnforceAsync();

        Assert.Equal(1, result.ExpiredCount);
        Assert.Equal(0, result.BlockedCount);

        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.False(statusB!.HasRegistered); // removed from group
        Assert.Equal(1, statusB.ViolationCount);
        Assert.False(statusB.IsRegistrationBlocked);

        // Warning email sent
        Assert.Single(_emailSender.SentMessages);
        Assert.Contains("Cảnh cáo", _emailSender.SentMessages[0].Subject);
    }

    [Fact]
    public async Task Enforcer_ThirdViolation_BlocksStudentRegistration()
    {
        _store.SetPenalty(StudentBId, violationCount: 2, isBlocked: false);

        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        _timeProvider.Advance(TimeSpan.FromHours(25));
        _emailSender.SentMessages.Clear();

        var enforcer = new TopicRegistrationExpirationEnforcer(
            _store,
            _studentInfoReader,
            _emailSender,
            _timeProvider,
            NullLogger<TopicRegistrationExpirationEnforcer>.Instance);

        var result = await enforcer.EnforceAsync();

        Assert.Equal(1, result.ExpiredCount);
        Assert.Equal(1, result.BlockedCount);

        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.Equal(3, statusB!.ViolationCount);
        Assert.True(statusB.IsRegistrationBlocked);

        // Block email sent
        Assert.Single(_emailSender.SentMessages);
        Assert.Contains("Khóa quyền", _emailSender.SentMessages[0].Subject);
    }

    [Fact]
    public async Task RaceCondition_ConcurrentRegistrations_ForSingleSlot_AllowsOnlyOneWinner()
    {
        // 10 students concurrently try to register for Topic 2 (StudentNumber = 1)
        var studentIds = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();
        foreach (var sid in studentIds)
        {
            _studentInfoReader.AddStudent(new StudentInfoContractDto(
                sid, $"student_{sid}@test.edu.vn", $"Student {sid}", $"SV_{sid:N}", null));
        }

        var tasks = studentIds.Select(sid =>
        {
            var handler = CreateRegisterHandler(sid);
            return handler.HandleAsync(new RegisterTopicCommand(TopicId2));
        }).ToList();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.IsSuccess);
        var conflictCount = results.Count(r => !r.IsSuccess && r.Error.Code == TopicRegistrationErrors.TopicAlreadyTaken.Code);

        Assert.Equal(1, successCount);
        Assert.Equal(9, conflictCount);
    }

    [Fact]
    public async Task Register_EmptyTopicId_ReturnsValidationError()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal("TopicRegistration.InvalidTopicId", result.Error.Code);
    }

    [Fact]
    public async Task Register_NonExistentTopic_ReturnsTopicNotFound()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_PartnerNotFound_ByUserCode_ReturnsPartnerNotFound()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerUserCode: "NON_EXISTENT_STUDENT"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.PartnerNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_WhitespacePartnerCode_TreatedAsSolo_Succeeds()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerUserCode: "   "));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPendingConfirmation);
    }

    [Fact]
    public async Task Register_InvitingSelf_ByUserCode_ReturnsCannotInviteSelf()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerUserCode: "SV001"));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.CannotInviteSelf.Code, result.Error.Code);
    }

    [Fact]
    public async Task Register_PartnerWithStudentCode_Succeeds()
    {
        var handler = CreateRegisterHandler(StudentAId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerUserCode: "SV002"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPendingConfirmation);

        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.Equal(nameof(MemberConfirmationStatus.Pending), statusB!.ConfirmationStatus);
    }

    [Fact]
    public async Task Register_Unauthenticated_ReturnsUnauthorized()
    {
        var handler = CreateRegisterHandler(Guid.Empty, new UnauthenticatedCurrentUser());
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Register_NotAnActiveStudent_ReturnsForbidden()
    {
        var nonStudentUserId = Guid.NewGuid();
        var handler = CreateRegisterHandler(nonStudentUserId);
        var result = await handler.HandleAsync(new RegisterTopicCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal("TopicRegistration.OnlyStudentsAllowed", result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Confirm_EmptyOrWhitespaceToken_ReturnsInvalidConfirmationToken(string token)
    {
        var handler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var result = await handler.HandleAsync(new ConfirmTopicRegistrationCommand(token));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.InvalidConfirmationToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task Confirm_AlreadyConfirmed_ReturnsAlreadyConfirmed()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        var token = _store.GetLastGeneratedRawToken()!;

        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var firstResult = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand(token));
        Assert.True(firstResult.IsSuccess);

        var secondResult = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand(token));
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.AlreadyConfirmed.Code, secondResult.Error.Code);
    }

    [Fact]
    public async Task Confirm_OtherStudentAttemptsToConfirm_ReturnsForbidden()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        var token = _store.GetLastGeneratedRawToken()!;

        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentCId),
            _timeProvider);

        var result = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand(token));

        Assert.False(result.IsSuccess);
        Assert.Equal("TopicRegistration.Forbidden", result.Error.Code);
    }

    [Fact]
    public async Task Confirm_TokenWithSurroundingWhitespace_Succeeds()
    {
        var regHandler = CreateRegisterHandler(StudentAId);
        await regHandler.HandleAsync(new RegisterTopicCommand(
            TopicId1, PartnerStudentId: StudentBId));

        var token = _store.GetLastGeneratedRawToken()!;

        var confirmHandler = new ConfirmTopicRegistrationHandler(
            _store,
            new FakeCurrentUser(StudentBId),
            _timeProvider);

        var result = await confirmHandler.HandleAsync(new ConfirmTopicRegistrationCommand($"  {token}  "));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetMyStatus_WhenNotRegistered_ReturnsHasRegisteredFalse()
    {
        var handler = new GetMyRegistrationStatusHandler(
            _store,
            new FakeCurrentUser(StudentAId));

        var result = await handler.HandleAsync(new GetMyRegistrationStatusQuery());

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.HasRegistered);
        Assert.Equal(0, result.Value.ViolationCount);
        Assert.False(result.Value.IsRegistrationBlocked);
    }

    [Fact]
    public async Task GetMyStatus_Unauthenticated_ReturnsUnauthorized()
    {
        var handler = new GetMyRegistrationStatusHandler(
            _store,
            new UnauthenticatedCurrentUser());

        var result = await handler.HandleAsync(new GetMyRegistrationStatusQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth.Unauthorized", result.Error.Code);
    }

    private RegisterTopicHandler CreateRegisterHandler(Guid currentUserId, ICurrentUser? customUser = null)
        => new(
            _store,
            _studentInfoReader,
            _emailSender,
            customUser ?? new FakeCurrentUser(currentUserId),
            _timeProvider);

    private sealed class UnauthenticatedCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string> { "Student" };
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStudentInfoReader : IStudentInfoReader
    {
        private readonly Dictionary<Guid, StudentInfoContractDto> _students = [];

        public void AddStudent(StudentInfoContractDto s) => _students[s.Id] = s;

        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
            IEnumerable<Guid> studentIds,
            CancellationToken cancellationToken = default)
        {
            var res = studentIds.Where(_students.ContainsKey).ToDictionary(id => id, id => _students[id]);
            return Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(res);
        }

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
            Guid? studentId,
            string? userCode,
            CancellationToken cancellationToken = default)
        {
            if (studentId.HasValue && _students.TryGetValue(studentId.Value, out var s))
            {
                return Task.FromResult<StudentInfoContractDto?>(s);
            }

            if (!string.IsNullOrWhiteSpace(userCode))
            {
                var byCode = _students.Values.FirstOrDefault(x => string.Equals(x.UserCode, userCode.Trim(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult<StudentInfoContractDto?>(byCode);
            }

            return Task.FromResult<StudentInfoContractDto?>(null);
        }
    }

    private sealed class FakeTimeProvider(DateTime initialUtc) : TimeProvider
    {
        private DateTime _utc = initialUtc;

        public override DateTimeOffset GetUtcNow() => _utc;

        public void Advance(TimeSpan delta) => _utc = _utc.Add(delta);
    }

    private sealed class FakeTopicRegistrationStore : ITopicRegistrationStore
    {
        private readonly object _sync = new();
        private readonly Dictionary<Guid, (Guid BatchId, int StudentNumber, string Status)> _topics = [];
        private readonly List<StudentGroup> _groups = [];
        private readonly Dictionary<Guid, StudentRegistrationPenalty> _penalties = [];
        private string? _lastRawToken;

        public bool IsRegistrationWindowOpen { get; set; } = true;

        public void SeedTopic(Guid topicId, Guid batchId, int studentNumber, string status)
        {
            _topics[topicId] = (batchId, studentNumber, status);
        }

        public void SetPenalty(Guid studentId, int violationCount, bool isBlocked)
        {
            var p = StudentRegistrationPenalty.Create(studentId);
            for (var i = 0; i < violationCount; i++)
            {
                p.RecordViolation(DateTime.UtcNow);
            }
            if (isBlocked && !p.IsRegistrationBlocked)
            {
                p.RecordViolation(DateTime.UtcNow, maxViolations: violationCount);
            }
            _penalties[studentId] = p;
        }

        public string? GetLastGeneratedRawToken() => _lastRawToken;

        public IReadOnlyList<StudentGroupMember> GetGroupMembers(Guid groupId)
            => _groups.FirstOrDefault(g => g.Id == groupId)?.Members.ToList() ?? [];

        public Task<Result<TopicRegistrationResultDto>> RegisterTopicAsync(
            Guid topicId,
            Guid studentAId,
            Guid? studentBId,
            DateTime nowUtc,
            TimeSpan confirmationWindow,
            string confirmationTokenHash,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (!_topics.TryGetValue(topicId, out var topic))
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound));
                }

                if (!string.Equals(topic.Status, "Published", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotPublished));
                }

                if (!IsRegistrationWindowOpen)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.RegistrationWindowClosed));
                }

                if (_penalties.TryGetValue(studentAId, out var penA) && penA.IsRegistrationBlocked)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentRegistrationBlocked));
                }

                if (studentBId.HasValue && _penalties.TryGetValue(studentBId.Value, out var penB) && penB.IsRegistrationBlocked)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.PartnerRegistrationBlocked));
                }

                // Check already registered in batch
                var alreadyRegistered = _groups.SelectMany(g => g.Members).Any(m =>
                    (m.StudentId == studentAId || (studentBId.HasValue && m.StudentId == studentBId.Value))
                    && m.BatchId == topic.BatchId);

                if (alreadyRegistered)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch));
                }

                var group = _groups.FirstOrDefault(g => g.TopicId == topicId);
                if (group is not null)
                {
                    // Clean up expired pending members
                    var expired = group.Members
                        .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending && m.ConfirmationExpiresAtUtc <= nowUtc)
                        .ToList();
                    foreach (var exp in expired)
                    {
                        group.RemoveMember(exp.StudentId);
                        if (!_penalties.TryGetValue(exp.StudentId, out var p))
                        {
                            p = StudentRegistrationPenalty.Create(exp.StudentId);
                            _penalties[exp.StudentId] = p;
                        }
                        p.RecordViolation(nowUtc);
                    }
                }

                var occupiedSlots = group?.Members.Count(m =>
                    m.ConfirmationStatus == MemberConfirmationStatus.Confirmed
                    || (m.ConfirmationStatus == MemberConfirmationStatus.Pending && m.ConfirmationExpiresAtUtc > nowUtc)) ?? 0;

                var requestedSlots = studentBId.HasValue ? 2 : 1;
                if (requestedSlots > topic.StudentNumber)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.GroupSizeExceedsTopicQuota));
                }

                if (occupiedSlots + requestedSlots > topic.StudentNumber)
                {
                    return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicAlreadyTaken));
                }

                if (group is null)
                {
                    group = new StudentGroup(Guid.NewGuid(), topicId, nowUtc);
                    _groups.Add(group);
                }

                group.AddConfirmedMember(studentAId, topic.BatchId, nowUtc);

                DateTime? expiresAt = null;
                if (studentBId.HasValue)
                {
                    expiresAt = nowUtc.Add(confirmationWindow);
                    _lastRawToken = "test_raw_token_" + Guid.NewGuid().ToString("N");
                    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_lastRawToken))).ToLowerInvariant();
                    group.AddPendingMember(studentBId.Value, topic.BatchId, studentAId, hash, nowUtc, expiresAt.Value);
                }

                return Task.FromResult(Result.Success(new TopicRegistrationResultDto(
                    group.Id,
                    topicId,
                    topic.BatchId,
                    studentBId.HasValue,
                    expiresAt,
                    "Success")));
            }
        }

        public Task<Result<ConfirmRegistrationResultDto>> ConfirmRegistrationAsync(
            string tokenHash,
            Guid? currentStudentId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var member = _groups.SelectMany(g => g.Members)
                    .FirstOrDefault(m => m.ConfirmationTokenHash == tokenHash);

                if (member is null)
                {
                    return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.InvalidConfirmationToken));
                }

                if (currentStudentId.HasValue && currentStudentId.Value != member.StudentId)
                {
                    return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(
                        Error.Forbidden("TopicRegistration.Forbidden", "Forbidden")));
                }

                if (member.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
                {
                    return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.AlreadyConfirmed));
                }

                if (member.IsExpired(nowUtc))
                {
                    return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.ConfirmationExpired));
                }

                member.Confirm(nowUtc);
                return Task.FromResult(Result.Success(new ConfirmRegistrationResultDto(
                    member.GroupId,
                    member.Group.TopicId,
                    member.StudentId,
                    nowUtc,
                    "Success")));
            }
        }

        public Task<IReadOnlyList<ExpiredPendingMemberDto>> ExpirePendingRegistrationsAsync(
            DateTime nowUtc,
            int maxViolationsBeforeBlock,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var expiredMembers = _groups.SelectMany(g => g.Members)
                    .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending
                                && m.ConfirmationExpiresAtUtc <= nowUtc)
                    .ToList();

                var results = new List<ExpiredPendingMemberDto>();
                foreach (var m in expiredMembers)
                {
                    m.Group.RemoveMember(m.StudentId);
                    if (!_penalties.TryGetValue(m.StudentId, out var pen))
                    {
                        pen = StudentRegistrationPenalty.Create(m.StudentId);
                        _penalties[m.StudentId] = pen;
                    }
                    pen.RecordViolation(nowUtc, maxViolationsBeforeBlock);
                    results.Add(new ExpiredPendingMemberDto(
                        m.GroupId,
                        m.StudentId,
                        null,
                        null,
                        m.Group.TopicId,
                        null,
                        pen.ViolationCount,
                        pen.IsRegistrationBlocked));
                }
                return Task.FromResult<IReadOnlyList<ExpiredPendingMemberDto>>(results);
            }
        }

        public Task<StudentRegistrationStatusDto?> GetStudentRegistrationStatusAsync(
            Guid studentId,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                var member = _groups.SelectMany(g => g.Members)
                    .OrderByDescending(m => m.JoinedAt)
                    .FirstOrDefault(m => m.StudentId == studentId);

                _penalties.TryGetValue(studentId, out var penalty);

                return Task.FromResult<StudentRegistrationStatusDto?>(new StudentRegistrationStatusDto(
                    HasRegistered: member is not null,
                    GroupId: member?.GroupId,
                    TopicId: member?.Group.TopicId,
                    TopicTitleVi: null,
                    ConfirmationStatus: member?.ConfirmationStatus.ToString(),
                    ConfirmationExpiresAtUtc: member?.ConfirmationExpiresAtUtc,
                    ViolationCount: penalty?.ViolationCount ?? 0,
                    IsRegistrationBlocked: penalty?.IsRegistrationBlocked ?? false,
                    BlockedAtUtc: penalty?.BlockedAtUtc));
            }
        }
    }
}

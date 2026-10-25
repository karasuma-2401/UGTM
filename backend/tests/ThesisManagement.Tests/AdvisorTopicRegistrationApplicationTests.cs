using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorUnassignTopicStudents;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class AdvisorTopicRegistrationApplicationTests
{
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid TopicId1 = Guid.NewGuid(); // Quota 2
    private static readonly Guid TopicId2 = Guid.NewGuid(); // Quota 1
    private static readonly Guid TopicIdOther = Guid.NewGuid(); // Other topic
    private static readonly Guid AdvisorId = Guid.NewGuid();
    private static readonly Guid OtherAdvisorId = Guid.NewGuid();
    private static readonly Guid StudentAId = Guid.NewGuid();
    private static readonly Guid StudentBId = Guid.NewGuid();
    private static readonly Guid StudentCId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeTopicRegistrationStore _store = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeTimeProvider _timeProvider = new(FixedNow);

    public AdvisorTopicRegistrationApplicationTests()
    {
        _store.SeedTopic(TopicId1, BatchId, studentNumber: 2, status: "Draft", advisorId: AdvisorId);
        _store.SeedTopic(TopicId2, BatchId, studentNumber: 1, status: "Submitted", advisorId: AdvisorId);
        _store.SeedTopic(TopicIdOther, BatchId, studentNumber: 2, status: "Draft", advisorId: OtherAdvisorId);

        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentAId, "studentA@test.edu.vn", "Nguyen Van A", "SV001", null));
        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentBId, "studentB@test.edu.vn", "Tran Thi B", "SV002", null));
        _studentInfoReader.AddStudent(new StudentInfoContractDto(
            StudentCId, "studentC@test.edu.vn", "Le Van C", "SV003", null));
    }

    [Fact]
    public async Task Assign_SingleStudent_ByAdvisor_InPreparationStage_Succeeds_AndConfirmedImmediately_EmailsSent()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "SV001" }));

        Assert.True(result.IsSuccess);
        Assert.Equal(TopicId1, result.Value.TopicId);
        Assert.Single(result.Value.AssignedStudentIds);
        Assert.Contains(StudentAId, result.Value.AssignedStudentIds);

        var status = await _store.GetStudentRegistrationStatusAsync(StudentAId);
        Assert.NotNull(status);
        Assert.True(status.HasRegistered);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), status.ConfirmationStatus);

        Assert.Single(_emailSender.SentMessages);
        Assert.Equal("studentA@test.edu.vn", _emailSender.SentMessages[0].To);
    }

    [Fact]
    public async Task Assign_TwoStudents_ByAdvisor_InPreparationStage_Succeeds_BothConfirmedImmediately()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "SV001", "SV002" }));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.AssignedStudentIds.Count);

        var statusA = await _store.GetStudentRegistrationStatusAsync(StudentAId);
        var statusB = await _store.GetStudentRegistrationStatusAsync(StudentBId);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), statusA!.ConfirmationStatus);
        Assert.Equal(nameof(MemberConfirmationStatus.Confirmed), statusB!.ConfirmationStatus);

        Assert.Equal(2, _emailSender.SentMessages.Count);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenUnauthenticated()
    {
        var handler = new AdvisorAssignTopicStudentsHandler(
            _store, _studentInfoReader, _emailSender, new FakeCurrentUser(null), _timeProvider);

        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_ForStudentRole()
    {
        var handler = CreateAssignHandler(StudentAId, "Student");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.OnlyAdvisorsAllowed.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenNotTopicAdvisor_AndNotAdmin()
    {
        var handler = CreateAssignHandler(OtherAdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.NotTopicAdvisor.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdmin_Succeeds_EvenIfNotAdvisor()
    {
        var adminId = Guid.NewGuid();
        var handler = CreateAssignHandler(adminId, "Admin");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenTopicNotFound()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            Guid.NewGuid(), StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenTopicPreparationWindowClosed()
    {
        _store.IsTopicPreparationStageActive = false;
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicPreparationWindowClosed.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("Published")]
    [InlineData("Rejected")]
    [InlineData("Closed")]
    public async Task Assign_ByAdvisor_Fails_WhenTopicInInvalidStatus(string invalidStatus)
    {
        var topicId = Guid.NewGuid();
        _store.SeedTopic(topicId, BatchId, studentNumber: 2, status: invalidStatus, advisorId: AdvisorId);

        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            topicId, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.InvalidTopicStatusForAdvisorAssign.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("NeedRevision")]
    [InlineData("Approved")]
    public async Task Assign_ByAdvisor_Succeeds_ForAllowedStatuses(string allowedStatus)
    {
        var topicId = Guid.NewGuid();
        _store.SeedTopic(topicId, BatchId, studentNumber: 2, status: allowedStatus, advisorId: AdvisorId);

        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            topicId, StudentCodes: new[] { "SV001" }));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenEmptyStudentList()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: Array.Empty<string>()));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.EmptyStudentList.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenDuplicateStudentInGroup()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001", "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.DuplicateStudentInGroup.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenGroupSizeExceedsQuota()
    {
        // TopicId2 has quota 1
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId2, StudentCodes: new[] { "SV001", "SV002" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.GroupSizeExceedsTopicQuota.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenStudentNotFound()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "NON_EXISTENT_MSSV" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenStudentIsBlocked()
    {
        _store.SetPenalty(StudentAId, violationCount: 3, isBlocked: true);

        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentRegistrationBlocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_Fails_WhenStudentAlreadyRegisteredInAnotherTopicInBatch()
    {
        // First, assign Student A to Topic 2
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var res1 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId2, StudentCodes: new[] { "SV001" }));
        Assert.True(res1.IsSuccess);

        // Now, try to assign Student A to Topic 1 as well
        var res2 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));
        Assert.False(res2.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch.Code, res2.Error.Code);
    }

    [Fact]
    public async Task Assign_ByAdvisor_UpdatesExistingGroup_ReplacesOldMembers_AndFreesRemovedStudents()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");

        // 1. Initially assign Student A to Topic 1
        var res1 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));
        Assert.True(res1.IsSuccess);

        // 2. Later, advisor replaces Student A with Student B
        var res2 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV002" }));
        Assert.True(res2.IsSuccess);

        var members = _store.GetGroupMembers(res2.Value.GroupId);
        Assert.Single(members);
        Assert.Equal(StudentBId, members[0].StudentId);

        // Student A is now free to be assigned elsewhere
        var res3 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId2, StudentCodes: new[] { "SV001" }));
        Assert.True(res3.IsSuccess);
    }

    [Fact]
    public async Task Unassign_ByAdvisor_Succeeds_RemovesGroup_AndFreesStudents()
    {
        var assignHandler = CreateAssignHandler(AdvisorId, "Lecturer");
        await assignHandler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        var unassignHandler = CreateUnassignHandler(AdvisorId, "Lecturer");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.True(result.IsSuccess);

        var statusA = await _store.GetStudentRegistrationStatusAsync(StudentAId);
        Assert.NotNull(statusA);
        Assert.False(statusA.HasRegistered);

        // Student A can now register elsewhere
        var res2 = await assignHandler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId2, StudentCodes: new[] { "SV001" }));
        Assert.True(res2.IsSuccess);
    }

    [Fact]
    public async Task Unassign_ByAdvisor_Fails_WhenNotAdvisor()
    {
        var assignHandler = CreateAssignHandler(AdvisorId, "Lecturer");
        await assignHandler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        var unassignHandler = CreateUnassignHandler(OtherAdvisorId, "Lecturer");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.NotTopicAdvisor.Code, result.Error.Code);
    }

    [Fact]
    public async Task Unassign_ByAdvisor_Fails_WhenNoRegistrationExists()
    {
        var unassignHandler = CreateUnassignHandler(AdvisorId, "Lecturer");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.RegistrationNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Unassign_ByAdvisor_Fails_WhenPreparationWindowClosed()
    {
        var assignHandler = CreateAssignHandler(AdvisorId, "Lecturer");
        await assignHandler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        _store.IsTopicPreparationStageActive = false;
        var unassignHandler = CreateUnassignHandler(AdvisorId, "Lecturer");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicPreparationWindowClosed.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_MixedCodeAndId_SameStudent_Fails_DuplicateStudentInGroup()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "SV001" },
            StudentIds: new[] { StudentAId }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.DuplicateStudentInGroup.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_StudentCodes_WithWhitespaceAndMixedCase_ResolvesCorrectly()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "  sv001  " }));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.AssignedStudentIds);
        Assert.Contains(StudentAId, result.Value.AssignedStudentIds);
    }

    [Fact]
    public async Task Assign_DuplicateCodes_DifferentCase_Fails_DuplicateStudentInGroup()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "SV001", "sv001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.DuplicateStudentInGroup.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_WithEmptyTopicId_Fails_TopicNotFound()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            Guid.Empty,
            StudentCodes: new[] { "SV001" }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_WithEmptyGuidStudentId_Fails_StudentNotFound()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentIds: new[] { Guid.Empty }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_WithWhitespaceStudentCode_Fails_StudentNotFound()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        var result = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1,
            StudentCodes: new[] { "   " }));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.StudentNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Assign_SameStudent_InDifferentBatch_Succeeds()
    {
        var handler = CreateAssignHandler(AdvisorId, "Lecturer");
        // Assign to Topic 1 in Batch 1
        var res1 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));
        Assert.True(res1.IsSuccess);

        // Seed Topic 3 in Batch 2
        var batch2Id = Guid.NewGuid();
        var topicInBatch2Id = Guid.NewGuid();
        _store.SeedTopic(topicInBatch2Id, batch2Id, studentNumber: 1, status: "Draft", advisorId: AdvisorId);

        // Assign to Topic in Batch 2 -> Allowed
        var res2 = await handler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            topicInBatch2Id, StudentCodes: new[] { "SV001" }));
        Assert.True(res2.IsSuccess);
    }

    [Fact]
    public async Task Unassign_ByAdmin_Succeeds_EvenIfNotTopicAdvisor()
    {
        var assignHandler = CreateAssignHandler(AdvisorId, "Lecturer");
        await assignHandler.HandleAsync(new AdvisorAssignTopicStudentsCommand(
            TopicId1, StudentCodes: new[] { "SV001" }));

        var adminId = Guid.NewGuid();
        var unassignHandler = CreateUnassignHandler(adminId, "Admin");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Unassign_WithEmptyTopicId_Fails_TopicNotFound()
    {
        var unassignHandler = CreateUnassignHandler(AdvisorId, "Lecturer");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.TopicNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Unassign_ByStudentRole_Fails_OnlyAdvisorsAllowed()
    {
        var unassignHandler = CreateUnassignHandler(StudentAId, "Student");
        var result = await unassignHandler.HandleAsync(new AdvisorUnassignTopicStudentsCommand(TopicId1));

        Assert.False(result.IsSuccess);
        Assert.Equal(TopicRegistrationErrors.OnlyAdvisorsAllowed.Code, result.Error.Code);
    }

    private AdvisorAssignTopicStudentsHandler CreateAssignHandler(Guid userId, string role)
        => new(
            _store,
            _studentInfoReader,
            _emailSender,
            new FakeCurrentUser(userId, role),
            _timeProvider);

    private AdvisorUnassignTopicStudentsHandler CreateUnassignHandler(Guid userId, string role)
        => new(
            _store,
            new FakeCurrentUser(userId, role),
            _timeProvider);

    private sealed class FakeStudentInfoReader : IStudentInfoReader
    {
        private readonly Dictionary<Guid, StudentInfoContractDto> _byId = [];
        private readonly Dictionary<string, StudentInfoContractDto> _byCode = new(StringComparer.OrdinalIgnoreCase);

        public void AddStudent(StudentInfoContractDto student)
        {
            _byId[student.Id] = student;
            if (!string.IsNullOrWhiteSpace(student.UserCode))
            {
                _byCode[student.UserCode] = student;
            }
        }

        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(
            IEnumerable<Guid> studentIds,
            CancellationToken cancellationToken = default)
        {
            var dict = studentIds
                .Distinct()
                .Where(_byId.ContainsKey)
                .ToDictionary(id => id, id => _byId[id]);
            return Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(dict);
        }

        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(
            Guid? studentId,
            string? userCode,
            CancellationToken cancellationToken = default)
        {
            if (studentId.HasValue && _byId.TryGetValue(studentId.Value, out var s))
            {
                return Task.FromResult<StudentInfoContractDto?>(s);
            }
            if (!string.IsNullOrWhiteSpace(userCode) && _byCode.TryGetValue(userCode.Trim(), out var s2))
            {
                return Task.FromResult<StudentInfoContractDto?>(s2);
            }
            return Task.FromResult<StudentInfoContractDto?>(null);
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> SentMessages { get; } = [];

        public Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            SentMessages.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(Guid? userId, params string[] roles) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        private DateTime _utc = utcNow;
        public override DateTimeOffset GetUtcNow() => _utc;
        public void Advance(TimeSpan delta) => _utc = _utc.Add(delta);
    }
}

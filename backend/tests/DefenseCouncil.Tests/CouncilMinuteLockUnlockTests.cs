using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilMinuteWorkspace;
using UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authentication;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Infrastructure.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;
using Xunit;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilMinuteLockUnlockTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeDefenseCouncilUnitOfWork _uow = new();
    private readonly FakeBatchReader _batchReader = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();
    private readonly FakeStudentInfoReader _studentInfoReader = new();
    private readonly FakeDefenseGroupContract _defenseGroupContract = new();
    private readonly FakeTopicDefenseContract _topicDefenseContract = new();
    private readonly FakeDefenseCouncilAuditOutbox _auditOutbox = new();
    private readonly FakeReauthenticationVerifier _reauthVerifier = new();
    private readonly FakeCurrentUser _fakeCurrentUser = new();

    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

        builder.Services.AddSingleton<ThesisGradeCalculator>();
        builder.Services.AddScoped<IGradeCalculationService, GradeCalculationService>();

        builder.Services.AddSingleton<ICouncilRepository>(_councilRepo);
        builder.Services.AddSingleton<IDefenseCouncilUnitOfWork>(_uow);
        builder.Services.AddSingleton<IKltnBatchReader>(_batchReader);
        builder.Services.AddSingleton<ICouncilUserInfoReader>(_userInfoReader);
        builder.Services.AddSingleton<IStudentInfoReader>(_studentInfoReader);
        builder.Services.AddSingleton<IDefenseGroupContract>(_defenseGroupContract);
        builder.Services.AddSingleton<ITopicDefenseContract>(_topicDefenseContract);
        builder.Services.AddSingleton<IDefenseCouncilAuditOutbox>(_auditOutbox);
        builder.Services.AddSingleton<IReauthenticationVerifier>(_reauthVerifier);
        builder.Services.AddScoped<IFinalSubmissionAuthorityReader, TestScoreAuthority>();

        builder.Services.AddScoped<CouncilMinuteWorkspaceLoader>();
        builder.Services.AddSingleton<ICouncilMinuteWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddSingleton<UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries.IScoreSummaryConfigurationReader, FakeMinuteConfigurationReader>();
        builder.Services.AddScoped<GetCouncilMinuteWorkspaceHandler>();
        builder.Services.AddScoped<SaveCouncilMinuteHandler>();
        builder.Services.AddScoped<LockCouncilMinuteHandler>();
        builder.Services.AddScoped<UnlockCouncilMinuteHandler>();
        builder.Services.AddSingleton<ICouncilScoreWriteGuard, TestCouncilScoreWriteGuard>();
        builder.Services.AddScoped<SubmitCouncilScoreHandler>();
        builder.Services.AddScoped<CheckCouncilDiscrepancyHandler>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddAuthentication(TestLockAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestLockAuthHandler>(TestLockAuthHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapCouncilEndpoints();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private void AuthenticateClient(Guid userId, string role = "Lecturer", params string[] permissions)
    {
        var token = permissions.Length > 0
            ? $"test.{userId:N}.{role}.{string.Join(",", permissions)}"
            : $"test.{userId:N}.{role}";

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private (Guid CouncilId, Guid AssignmentId, Guid GroupId, Guid ChairmanId, Guid SecretaryId, Guid MemberId, Guid MemberUserId, Guid AdvisorUserId, Guid SecretaryUserId, Guid ChairmanUserId, Guid Crit1Id, Guid Crit2Id)
        SetupActiveCouncilWithReadyScores()
    {
        var batchId = Guid.NewGuid();
        var council = Council.Create(batchId, "HĐ_LOCK_01", DateOnly.FromDateTime(DateTime.Today.AddDays(7)), "08:00 - 11:30", "A101");
        council.Approve("QD-LOCK-01", DateOnly.FromDateTime(DateTime.Today), null, Guid.NewGuid(), DateTime.UtcNow);
        _councilRepo.Councils.Add(council);

        var crit1 = new GradingCriteria { Id = Guid.NewGuid(), BatchId = batchId, CriteriaCode = "C1", CriteriaName = "Nội dung", MaxScore = 6.0m, OrderIndex = 1 };
        var crit2 = new GradingCriteria { Id = Guid.NewGuid(), BatchId = batchId, CriteriaCode = "C2", CriteriaName = "Trình bày", MaxScore = 4.0m, OrderIndex = 2 };
        _councilRepo.GradingCriteriaList.AddRange([crit1, crit2]);

        var chairmanUserId = Guid.NewGuid();
        var secretaryUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();

        _userInfoReader.Users[chairmanUserId] = new CouncilUserInfoContractDto(chairmanUserId, "GS. Nguyễn Văn A", "nva@school.edu.vn", "GV001", true);
        _userInfoReader.Users[secretaryUserId] = new CouncilUserInfoContractDto(secretaryUserId, "TS. Trần Thị B", "ttb@school.edu.vn", "GV002", true);
        _userInfoReader.Users[memberUserId] = new CouncilUserInfoContractDto(memberUserId, "ThS. Lê Văn C", "lvc@school.edu.vn", "GV003", true);

        var chairman = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = chairmanUserId, RoleInCouncil = CouncilMemberRole.Chairman };
        var secretary = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = secretaryUserId, RoleInCouncil = CouncilMemberRole.Secretary };
        var member = new CouncilMember { Id = Guid.NewGuid(), CouncilId = council.Id, UserId = memberUserId, RoleInCouncil = CouncilMemberRole.Member };
        _councilRepo.Members.AddRange([chairman, secretary, member]);

        var groupId = Guid.NewGuid();
        var advisorUserId = Guid.NewGuid();
        _userInfoReader.Users[advisorUserId] = new CouncilUserInfoContractDto(advisorUserId, "PGS. Phạm Văn D", "pvd@school.edu.vn", "GV004", true);

        var assignment = new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            GroupId = groupId,
            OrderNumber = 1
        };
        _councilRepo.Assignments.Add(assignment);

        var topicId = Guid.NewGuid();
        _defenseGroupContract.Groups[groupId] = new DefenseGroupSnapshotContractDto(
            groupId,
            topicId,
            batchId,
            "Accepted",
            DateTime.UtcNow,
            [new DefenseGroupMemberContractDto(Guid.NewGuid(), batchId, "Confirmed")]);

        _topicDefenseContract.Topics[topicId] = new TopicDefenseDetailsContractDto(
            topicId,
            batchId,
            advisorUserId,
            "Hệ thống Quản lý KLTN",
            "Thesis Management System");

        // Reviewer is council member (Member role)
        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(),
            groupId,
            memberUserId,
            advisorUserId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Guid.NewGuid(),
            DateTime.UtcNow);

        var revEvaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 8.0m, "Phản biện tốt", DefenseApprovalStatus.Approved, memberUserId, DateTime.UtcNow);
        revAssignment.GetType().GetProperty("Evaluation")?.SetValue(revAssignment, revEvaluation);
        _councilRepo.ActiveReviewerAssignments[groupId] = revAssignment;

        var advEvaluation = AdvisorEvaluation.Create(
            Guid.NewGuid(),
            groupId,
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            8.0m,
            "Hướng dẫn tốt",
            advisorUserId,
            DateTime.UtcNow);
        _councilRepo.AdvisorEvaluations[groupId] = advEvaluation;

        // Scores from Chairman and Secretary
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assignment.Id,
            CouncilMemberId = chairman.Id,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        });
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = assignment.Id,
            CouncilMemberId = secretary.Id,
            Score = 8.0m,
            EvaluatedAt = DateTime.UtcNow
        });

        return (council.Id, assignment.Id, groupId, chairman.Id, secretary.Id, member.Id, memberUserId, advisorUserId, secretaryUserId, chairmanUserId, crit1.Id, crit2.Id);
    }

    private LockCouncilMinuteHandler CreateLockHandler(ICurrentUser currentUser)
    {
        var workspaceLoader = new CouncilMinuteWorkspaceLoader(
            _councilRepo,
            _userInfoReader,
            _defenseGroupContract,
            _topicDefenseContract,
            new FakeMinuteConfigurationReader());

        return new LockCouncilMinuteHandler(
            _councilRepo,
            _uow,
            workspaceLoader,
            _reauthVerifier,
            _auditOutbox,
            currentUser,
            new TestCouncilScoreWriteGuard(),
            new TestScoreAuthority(currentUser));
    }

    private UnlockCouncilMinuteHandler CreateUnlockHandler(ICurrentUser currentUser)
    {
        var authorityReader = new TestScoreAuthority(currentUser);
        return new UnlockCouncilMinuteHandler(
            _councilRepo,
            _uow,
            authorityReader,
            _auditOutbox,
            currentUser,
            new TestCouncilScoreWriteGuard());
    }

    // =========================================================================
    // 1. Lock Council Minute - Unit Tests
    // =========================================================================

    [Fact]
    public async Task LockMinute_SecretaryWithValidPassword_LocksMinuteAndEmitsAudit()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };
        _fakeCurrentUser.Permissions = new HashSet<string>();

        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPassword123!");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Locked", result.Value.LockStatus);
        Assert.NotNull(result.Value.LockedAt);
        Assert.Equal(ids.SecretaryUserId, result.Value.LockedBy);

        var minute = _councilRepo.Minutes.FirstOrDefault(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.NotNull(minute);
        Assert.Equal(CouncilMinuteLockStatus.Locked, minute.LockStatus);
        Assert.Equal(ids.SecretaryUserId, minute.LockedBy);
        Assert.NotNull(minute.LockedAt);

        Assert.Contains(_auditOutbox.Entries, e => e.Entry.Action == "DefenseCouncil.CouncilMinute.Locked");
    }

    [Fact]
    public async Task LockMinute_AdminWithValidPassword_LocksMinuteSuccessfully()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        var adminId = Guid.NewGuid();
        _fakeCurrentUser.UserId = adminId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };

        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "AdminSecret456!");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Locked", result.Value.LockStatus);
        Assert.Equal(adminId, result.Value.LockedBy);
    }

    [Fact]
    public async Task LockMinute_UserNotSecretaryOrAdmin_ReturnsForbidden()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        // Chairman tries to lock
        _fakeCurrentUser.UserId = ids.ChairmanUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ChairmanPass123!");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.OnlySecretaryCanLockMinute.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LockMinute_EmptyPassword_ReturnsBadRequest(string? password)
    {
        var ids = SetupActiveCouncilWithReadyScores();

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, password!);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.PasswordRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_InvalidPassword_ReturnsUnauthorized()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.InvalidCredentials;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "WrongPassword");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ReauthenticationFailed.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_AccountLocked_ReturnsLockedOutStatus()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.AccountLocked;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "AnyPassword");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ReauthenticationLocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_CouncilNotActive_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        var council = _councilRepo.Councils.First(c => c.Id == ids.CouncilId);
        council.GetType().GetProperty("Status")?.SetValue(council, CouncilStatus.Draft);

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPass");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotActiveForScoring.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_MinuteNotReadyToLock_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        // Remove Chairman score so isReadyToLock becomes false
        _councilRepo.Scores.RemoveAll(s => s.CouncilMemberId == ids.ChairmanId);

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };
        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPass");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MinuteNotReadyToLock.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_AlreadyLocked_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedAt = DateTime.UtcNow,
            LockedBy = ids.SecretaryUserId
        });

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };
        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPass");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MinuteAlreadyLocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_ScoreSummaryAlreadyPublished_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.ScoreSummaries[ids.GroupId] = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = ids.GroupId,
            Status = ScoreSummaryStatus.PublishedToStudent
        };

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };
        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPass");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ScoresAlreadyPublished.Code, result.Error.Code);
    }

    [Fact]
    public async Task LockMinute_PreviouslyUnlockedByOverride_CanBeLockedAgain()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.UnlockedByOverride,
            OverrideReason = "Mo khoa de chinh sua diem",
            UnlockedAt = DateTime.UtcNow.AddMinutes(-10),
            UnlockedBy = Guid.NewGuid()
        });

        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };
        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        var handler = CreateLockHandler(_fakeCurrentUser);
        var command = new LockCouncilMinuteCommand(ids.AssignmentId, "ValidPass");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Locked", result.Value.LockStatus);
        Assert.Equal(ids.SecretaryUserId, result.Value.LockedBy);

        var minute = _councilRepo.Minutes.First(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.Equal(CouncilMinuteLockStatus.Locked, minute.LockStatus);
    }

    // =========================================================================
    // 2. Unlock Council Minute - Unit Tests
    // =========================================================================

    [Fact]
    public async Task UnlockMinute_FacultyWithPermission_UnlocksAndEmitsAudit()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedAt = DateTime.UtcNow.AddHours(-1),
            LockedBy = ids.SecretaryUserId
        });

        var facultyLeaderId = Guid.NewGuid();
        _fakeCurrentUser.UserId = facultyLeaderId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Faculty" };
        _fakeCurrentUser.Permissions = new HashSet<string> { PermissionCodes.CouncilMinutesUnlock };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Khiếu nại điểm số của sinh viên cần mở khóa để chấm lại.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("UnlockedByOverride", result.Value.LockStatus);
        Assert.Null(result.Value.LockedAt);
        Assert.Null(result.Value.LockedBy);
        Assert.Equal("Khiếu nại điểm số của sinh viên cần mở khóa để chấm lại.", result.Value.OverrideReason);

        var minute = _councilRepo.Minutes.First(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.Equal(CouncilMinuteLockStatus.UnlockedByOverride, minute.LockStatus);
        Assert.Null(minute.LockedAt);
        Assert.Null(minute.LockedBy);
        Assert.Equal(facultyLeaderId, minute.UnlockedBy);
        Assert.NotNull(minute.UnlockedAt);
        Assert.Equal("Khiếu nại điểm số của sinh viên cần mở khóa để chấm lại.", minute.OverrideReason);

        Assert.Contains(_auditOutbox.Entries, e => e.Entry.Action == "DefenseCouncil.CouncilMinute.UnlockGranted");
    }

    [Fact]
    public async Task UnlockMinute_AdminRole_UnlocksMinuteSuccessfully()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked,
            LockedAt = DateTime.UtcNow.AddHours(-1),
            LockedBy = ids.SecretaryUserId
        });

        var adminId = Guid.NewGuid();
        _fakeCurrentUser.UserId = adminId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };
        _fakeCurrentUser.Permissions = new HashSet<string>();

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Admin mở khóa khẩn cấp do sự cố kỹ thuật hệ thống.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("UnlockedByOverride", result.Value.LockStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ngắn")] // < 10 chars
    public async Task UnlockMinute_InvalidReasonLength_ReturnsBadRequest(string? reason)
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        _fakeCurrentUser.UserId = Guid.NewGuid();
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(ids.AssignmentId, reason!);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.OverrideReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task UnlockMinute_ReasonExceeds1000Chars_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        _fakeCurrentUser.UserId = Guid.NewGuid();
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(ids.AssignmentId, new string('A', 1001));

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.OverrideReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task UnlockMinute_UserWithoutUnlockPermission_ReturnsForbidden()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        // Faculty without councils.minutes.unlock permission
        _fakeCurrentUser.UserId = Guid.NewGuid();
        _fakeCurrentUser.Roles = new HashSet<string> { "Faculty" };
        _fakeCurrentUser.Permissions = new HashSet<string>();

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Lý do giải trình hợp lệ hơn mười ký tự.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.UnlockForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task UnlockMinute_LecturerOrSecretary_ReturnsForbidden()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        // Secretary tries to unlock
        _fakeCurrentUser.UserId = ids.SecretaryUserId;
        _fakeCurrentUser.Roles = new HashSet<string> { "Lecturer" };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Lý do giải trình hợp lệ hơn mười ký tự.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.UnlockForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task UnlockMinute_MinuteNotLocked_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        // Minute is Open (not Locked)
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Open
        });

        _fakeCurrentUser.UserId = Guid.NewGuid();
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Lý do giải trình hợp lệ hơn mười ký tự.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MinuteNotLocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task UnlockMinute_ScoreSummaryAlreadyPublished_ReturnsConflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });
        _councilRepo.ScoreSummaries[ids.GroupId] = new ScoreSummary
        {
            Id = Guid.NewGuid(),
            GroupId = ids.GroupId,
            Status = ScoreSummaryStatus.PublishedToStudent
        };

        _fakeCurrentUser.UserId = Guid.NewGuid();
        _fakeCurrentUser.Roles = new HashSet<string> { "Admin" };

        var handler = CreateUnlockHandler(_fakeCurrentUser);
        var command = new UnlockCouncilMinuteCommand(
            ids.AssignmentId,
            "Lý do giải trình hợp lệ hơn mười ký tự.");

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ScoresAlreadyPublished.Code, result.Error.Code);
    }

    // =========================================================================
    // 3. E2E HTTP Endpoints Integration Flow
    // =========================================================================

    [Fact]
    public async Task E2E_MinuteLockUnlockAndReLockLifecycle_Succeeds()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _reauthVerifier.DefaultOutcome = ReauthenticationOutcome.Verified;

        // 1. Wrong password returns 401
        _reauthVerifier.CustomOutcomes[(ids.SecretaryUserId, "WrongPass")] = ReauthenticationOutcome.InvalidCredentials;
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");

        var badPassResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("WrongPass"));
        Assert.Equal(HttpStatusCode.Unauthorized, badPassResponse.StatusCode);

        // 2. Lock minute with valid password returns 200 OK
        var lockResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Secret123!"));
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        var lockData = await ReadDataAsync(lockResponse);
        Assert.Equal("Locked", lockData.GetProperty("lockStatus").GetString());

        // 3. Try locking again returns 409 Conflict
        var secondLockResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Secret123!"));
        Assert.Equal(HttpStatusCode.Conflict, secondLockResponse.StatusCode);

        // 4. Secretary attempts to unlock -> returns 403 Forbidden
        var forbiddenUnlockResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa đặc biệt"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUnlockResponse.StatusCode);

        // 5. Faculty leader with councils.minutes.unlock permission unlocks minute
        var facultyLeaderId = Guid.NewGuid();
        AuthenticateClient(facultyLeaderId, "Faculty", PermissionCodes.CouncilMinutesUnlock);

        var unlockResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Điều chỉnh lại điểm thành phần theo khiếu nại"));
        Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);

        var unlockData = await ReadDataAsync(unlockResponse);
        Assert.Equal("UnlockedByOverride", unlockData.GetProperty("lockStatus").GetString());
        Assert.Equal("Điều chỉnh lại điểm thành phần theo khiếu nại", unlockData.GetProperty("overrideReason").GetString());

        // 6. Secretary can lock the minute again
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var reLockResponse = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Secret123!"));
        Assert.Equal(HttpStatusCode.OK, reLockResponse.StatusCode);

        var reLockData = await ReadDataAsync(reLockResponse);
        Assert.Equal("Locked", reLockData.GetProperty("lockStatus").GetString());
    }

    // =========================================================================
    // 4. QA Negative & Boundary: Path & Route Constraints
    // =========================================================================

    [Fact]
    public async Task QA_LockMinute_InvalidGuidInUrl_ReturnsNotFound()
    {
        AuthenticateClient(Guid.NewGuid(), "Lecturer");
        var response = await _client.PostAsJsonAsync(
            "/api/councils/assignments/not-a-valid-guid/minute/lock",
            new LockCouncilMinuteInputDto("Password123!"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QA_UnlockMinute_InvalidGuidInUrl_ReturnsNotFound()
    {
        AuthenticateClient(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            "/api/councils/assignments/invalid-guid/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa giải trình"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task QA_LockMinute_NonExistentAssignment_Returns404NotFound()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");

        var nonExistentAssignmentId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{nonExistentAssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Password123!"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.AssignmentNotFound.Code, json);
    }

    [Fact]
    public async Task QA_UnlockMinute_NonExistentAssignment_Returns404NotFound()
    {
        AuthenticateClient(Guid.NewGuid(), "Admin");

        var nonExistentAssignmentId = Guid.NewGuid();
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{nonExistentAssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa giải trình hợp lệ"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.AssignmentNotFound.Code, json);
    }

    // =========================================================================
    // 5. QA Negative & Boundary: Request Body & Malformed JSON & Data Types
    // =========================================================================

    [Fact]
    public async Task QA_LockMinute_EmptyRequestBody_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");

        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/lock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_LockMinute_MalformedJson_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");

        var content = new StringContent("{\"password\": ", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/lock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_LockMinute_WrongDataTypeForPassword_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");

        var content = new StringContent("{\"password\": 123456}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/lock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_UnlockMinute_EmptyRequestBody_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(Guid.NewGuid(), "Admin");

        var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/unlock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_UnlockMinute_MalformedJson_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(Guid.NewGuid(), "Admin");

        var content = new StringContent("{\"overrideReason\": ", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/unlock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task QA_UnlockMinute_WrongDataTypeForReason_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        AuthenticateClient(Guid.NewGuid(), "Admin");

        var content = new StringContent("{\"overrideReason\": [\"reason1\", \"reason2\"]}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"/api/councils/assignments/{ids.AssignmentId}/minute/unlock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // =========================================================================
    // 6. QA Negative & Boundary: Boundary Length for overrideReason
    // =========================================================================

    [Theory]
    [InlineData("123456789")] // 9 chars
    [InlineData("   123456789   ")] // 9 non-space chars surrounded by spaces
    public async Task QA_UnlockMinute_ReasonTrimmedBelow10Chars_ReturnsBadRequest(string reason)
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        AuthenticateClient(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto(reason));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.OverrideReasonRequired.Code, json);
    }

    [Fact]
    public async Task QA_UnlockMinute_ReasonExactly10Chars_Succeeds()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        AuthenticateClient(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("1234567890")); // exactly 10 chars

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal("1234567890", data.GetProperty("overrideReason").GetString());
    }

    [Fact]
    public async Task QA_UnlockMinute_ReasonExactly500Chars_Succeeds()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        AuthenticateClient(Guid.NewGuid(), "Admin");
        var maxReason = new string('X', 500);
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto(maxReason));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ReadDataAsync(response);
        Assert.Equal(maxReason, data.GetProperty("overrideReason").GetString());
    }

    [Fact]
    public async Task QA_UnlockMinute_ReasonExceeds500CharsByOne_ReturnsBadRequest()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        AuthenticateClient(Guid.NewGuid(), "Admin");
        var tooLong = new string('Y', 501);
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto(tooLong));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.OverrideReasonRequired.Code, json);
    }

    // =========================================================================
    // 7. QA Negative: Authentication & Authorization Permissions
    // =========================================================================

    [Fact]
    public async Task QA_LockMinute_AnonymousUser_Returns401Unauthorized()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Password123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QA_UnlockMinute_AnonymousUser_Returns401Unauthorized()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do giải trình hợp lệ"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Chairman")]
    [InlineData("Member")]
    [InlineData("Advisor")]
    [InlineData("OutsideLecturer")]
    [InlineData("Student")]
    public async Task QA_LockMinute_NonSecretaryNonAdmin_Returns403Forbidden(string callerRole)
    {
        var ids = SetupActiveCouncilWithReadyScores();

        Guid userId = callerRole switch
        {
            "Chairman" => ids.ChairmanUserId,
            "Member" => ids.MemberUserId,
            "Advisor" => ids.AdvisorUserId,
            "Student" => Guid.NewGuid(),
            _ => Guid.NewGuid()
        };

        string systemRole = callerRole == "Student" ? "Student" : "Lecturer";
        AuthenticateClient(userId, systemRole);

        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("Password123!"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.OnlySecretaryCanLockMinute.Code, json);
    }

    [Theory]
    [InlineData("Secretary")]
    [InlineData("Chairman")]
    [InlineData("Member")]
    [InlineData("Advisor")]
    [InlineData("Student")]
    [InlineData("FacultyWithoutPerm")]
    public async Task QA_UnlockMinute_ForbiddenRoles_Returns403Forbidden(string callerRole)
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        Guid userId = callerRole switch
        {
            "Secretary" => ids.SecretaryUserId,
            "Chairman" => ids.ChairmanUserId,
            "Member" => ids.MemberUserId,
            "Advisor" => ids.AdvisorUserId,
            "Student" => Guid.NewGuid(),
            _ => Guid.NewGuid()
        };

        string systemRole = callerRole switch
        {
            "Student" => "Student",
            "FacultyWithoutPerm" => "Faculty",
            _ => "Lecturer"
        };

        AuthenticateClient(userId, systemRole);

        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa đặc biệt"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.UnlockForbidden.Code, json);
    }

    // =========================================================================
    // 8. QA Negative: Business Logic & State Conflict (409)
    // =========================================================================

    [Fact]
    public async Task QA_LockMinute_CouncilInDraft_Returns409Conflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        var council = _councilRepo.Councils.First(c => c.Id == ids.CouncilId);
        council.GetType().GetProperty("Status")?.SetValue(council, CouncilStatus.Draft);

        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.NotActiveForScoring.Code, json);
    }

    [Fact]
    public async Task QA_LockMinute_MissingScores_Returns409Conflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();
        // Remove Secretary score so isReadyToLock is false
        _councilRepo.Scores.RemoveAll(s => s.CouncilMemberId == ids.SecretaryId);

        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.MinuteNotReadyToLock.Code, json);
    }

    [Fact]
    public async Task QA_LockMinute_UnresolvedDiscrepancyGreaterOrEqualTwo_Returns409Conflict()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        // Chairman gives 9.0, Secretary gives 6.5 -> diff = 2.5 >= 2.0 without targetComponentCode
        var chScore = _councilRepo.Scores.First(s => s.CouncilMemberId == ids.ChairmanId);
        chScore.Score = 9.0m;
        var secScore = _councilRepo.Scores.First(s => s.CouncilMemberId == ids.SecretaryId);
        secScore.Score = 6.5m;

        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.MinuteNotReadyToLock.Code, json);
    }

    [Fact]
    public async Task QA_LockMinute_ResolvedDiscrepancy_Succeeds()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        // Chairman gives 9.0, Secretary gives 6.5 -> diff = 2.5 >= 2.0
        var chScore = _councilRepo.Scores.First(s => s.CouncilMemberId == ids.ChairmanId);
        chScore.Score = 9.0m;
        var secScore = _councilRepo.Scores.First(s => s.CouncilMemberId == ids.SecretaryId);
        secScore.Score = 6.5m;

        // Secretary resolves discrepancy by saving conclusion with TargetComponentCode
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var saveResp = await _client.PutAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto(
                SecretaryMarking: "RejectedAsPerMinute",
                TargetComponentCode: "COUNCIL_CHAIRMAN",
                StudentFeedback: "Góp ý cho sinh viên.",
                Reason: "Loại bỏ điểm bất thường của chủ tịch theo biên bản."));
        Assert.Equal(HttpStatusCode.OK, saveResp.StatusCode);

        // Now lock should succeed!
        var lockResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));

        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);
        var data = await ReadDataAsync(lockResp);
        Assert.Equal("Locked", data.GetProperty("lockStatus").GetString());
    }

    [Theory]
    [InlineData(CouncilMinuteLockStatus.Open)]
    [InlineData(CouncilMinuteLockStatus.AllScoresEntered)]
    [InlineData(CouncilMinuteLockStatus.UnlockedByOverride)]
    public async Task QA_UnlockMinute_WhenNotLocked_Returns400BadRequest(CouncilMinuteLockStatus status)
    {
        var ids = SetupActiveCouncilWithReadyScores();
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = ids.AssignmentId,
            LockStatus = status
        });

        AuthenticateClient(Guid.NewGuid(), "Admin");
        var response = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Lý do mở khóa giải trình hợp lệ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.MinuteNotLocked.Code, json);
    }

    // =========================================================================
    // 9. QA Integration: State Protection on Downstream APIs (Scoring & Minute)
    // =========================================================================

    [Fact]
    public async Task QA_ScoringAndMinuteSave_BlockedWhenLocked_AllowedWhenUnlocked()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        // 1. Lock the minute
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var lockResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));
        Assert.Equal(HttpStatusCode.OK, lockResp.StatusCode);

        // 2. Chairman attempts to submit new score -> 409 Conflict (MinuteLocked)
        AuthenticateClient(ids.ChairmanUserId, "Lecturer");
        var scoreResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/scores",
            new SubmitCouncilScoreInputDto(
                [new CriterionScoreInputDto(ids.Crit1Id, 5.0m), new CriterionScoreInputDto(ids.Crit2Id, 3.5m)],
                "Cập nhật điểm sau bảo vệ"));
        Assert.Equal(HttpStatusCode.Conflict, scoreResp.StatusCode);
        var scoreErr = await scoreResp.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.MinuteLocked.Code, scoreErr);

        // 3. Secretary attempts to save minute -> 409 Conflict (MinuteLocked)
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var minuteResp = await _client.PutAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto(SecretaryMarking: "AcceptedAsPerMinute"));
        Assert.Equal(HttpStatusCode.Conflict, minuteResp.StatusCode);
        var minuteErr = await minuteResp.Content.ReadAsStringAsync();
        Assert.Contains(CouncilErrors.MinuteLocked.Code, minuteErr);

        // 4. Faculty opens override unlock
        var facultyId = Guid.NewGuid();
        AuthenticateClient(facultyId, "Faculty", PermissionCodes.CouncilMinutesUnlock);
        var unlockResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto("Cho phép hội đồng nhập lại điểm theo quyết định của Khoa"));
        Assert.Equal(HttpStatusCode.OK, unlockResp.StatusCode);

        // 5. Chairman submits new score again -> Now 200 OK!
        AuthenticateClient(ids.ChairmanUserId, "Lecturer");
        var reScoreResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/scores",
            new SubmitCouncilScoreInputDto(
                [new CriterionScoreInputDto(ids.Crit1Id, 5.5m), new CriterionScoreInputDto(ids.Crit2Id, 3.5m)],
                "Đã sửa điểm sau khi mở khóa"));
        Assert.Equal(HttpStatusCode.OK, reScoreResp.StatusCode);

        // Verify lock status remains UnlockedByOverride
        var minute = _councilRepo.Minutes.First(m => m.CouncilAssignmentId == ids.AssignmentId);
        Assert.Equal(CouncilMinuteLockStatus.UnlockedByOverride, minute.LockStatus);

        // 6. Secretary saves minute update -> Now 200 OK!
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        var reMinuteResp = await _client.PutAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute",
            new SaveCouncilMinuteInputDto(
                SecretaryMarking: "AcceptedAsPerMinute",
                StudentFeedback: "Điểm đã được hiệu chỉnh chính thức."));
        Assert.Equal(HttpStatusCode.OK, reMinuteResp.StatusCode);

        // 7. Secretary re-locks the minute -> 200 OK!
        var reLockResp = await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("ValidPass123!"));
        Assert.Equal(HttpStatusCode.OK, reLockResp.StatusCode);
        var reLockData = await ReadDataAsync(reLockResp);
        Assert.Equal("Locked", reLockData.GetProperty("lockStatus").GetString());
    }

    // =========================================================================
    // 10. QA Security & Audit Integrity: No Password Leaks in Audit
    // =========================================================================

    [Fact]
    public async Task QA_AuditLogging_DoesNotLeakPassword_AndCapturesSensitiveMetadata()
    {
        var ids = SetupActiveCouncilWithReadyScores();

        // Lock with a sensitive password
        AuthenticateClient(ids.SecretaryUserId, "Lecturer");
        await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/lock",
            new LockCouncilMinuteInputDto("SuperSensitiveSecretPassword999!"));

        // Check audit entries: no plain text password should exist in any serialized metadata or string representation
        foreach (var entry in _auditOutbox.Entries)
        {
            var serialized = JsonSerializer.Serialize(entry.Entry.Metadata);
            Assert.DoesNotContain("SuperSensitiveSecretPassword999!", serialized);
            Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        }

        // Faculty unlocks with sensitive explanation
        var facultyId = Guid.NewGuid();
        AuthenticateClient(facultyId, "Faculty", PermissionCodes.CouncilMinutesUnlock);
        var sensitiveReason = "Giải trình sự cố nhập nhầm điểm của giảng viên A theo biên bản cuộc họp khoa";
        await _client.PostAsJsonAsync(
            $"/api/councils/assignments/{ids.AssignmentId}/minute/unlock",
            new UnlockCouncilMinuteInputDto(sensitiveReason));

        Assert.Contains(_auditOutbox.Entries, e => e.Entry.Action == "DefenseCouncil.CouncilMinute.UnlockGranted");
        var unlockAudit = _auditOutbox.Entries.First(e => e.Entry.Action == "DefenseCouncil.CouncilMinute.UnlockGranted");
        var unlockMetadataStr = JsonSerializer.Serialize(
            unlockAudit.Entry.Metadata,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains(sensitiveReason, unlockMetadataStr);
        Assert.Contains(ids.AssignmentId.ToString(), unlockMetadataStr);
        Assert.Contains(facultyId.ToString(), unlockMetadataStr);
    }

    private static async Task<JsonElement> ReadDataAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").Clone();
    }

    private sealed class TestLockAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "LockTestAuthScheme";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var header = Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer test.", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = header["Bearer test.".Length..].Split('.', 3);
            if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token."));
            }

            var role = parts[1];
            var claims = new List<Claim>
            {
                new("sub", userId.ToString()),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role),
                new("role", role)
            };

            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                foreach (var perm in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    claims.Add(new Claim("permission", perm.Trim()));
                }
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}

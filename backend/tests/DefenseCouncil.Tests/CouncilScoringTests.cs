using UGTM.Modules.DefenseCouncil.Application.Councils;
using UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.Identity.Contracts.Users;
using Xunit;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilScoringTests
{
    private readonly FakeCouncilRepository _councilRepo = new();
    private readonly FakeDefenseCouncilUnitOfWork _uow = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly ThesisGradeCalculator _gradeCalculator = new();
    private readonly FakeCouncilUserInfoReader _userInfoReader = new();

    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _councilId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _assignmentId = Guid.NewGuid();

    private readonly Guid _chairmanUserId = Guid.NewGuid();
    private readonly Guid _secretaryUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _reviewerUserId = Guid.NewGuid();

    private readonly Guid _crit1Id = Guid.NewGuid();
    private readonly Guid _crit2Id = Guid.NewGuid();

    private Council _council = null!;
    private CouncilMember _chairmanMember = null!;
    private CouncilMember _secretaryMember = null!;
    private CouncilMember _regularMember = null!;
    private CouncilAssignment _assignment = null!;

    public CouncilScoringTests()
    {
        SetupData();
    }

    private void SetupData()
    {
        _council = new Council
        {
            Id = _councilId,
            BatchId = _batchId,
            CouncilCode = "HD01",
            DefenseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            TimeSlot = "08:00 - 11:30",
            Room = "A101",
            Status = CouncilStatus.Active
        };
        _councilRepo.Councils.Add(_council);

        _chairmanMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _chairmanUserId,
            RoleInCouncil = CouncilMemberRole.Chairman
        };
        _secretaryMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _secretaryUserId,
            RoleInCouncil = CouncilMemberRole.Secretary
        };
        _regularMember = new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = _councilId,
            UserId = _memberUserId,
            RoleInCouncil = CouncilMemberRole.Member
        };
        _councilRepo.Members.AddRange([_chairmanMember, _secretaryMember, _regularMember]);

        _assignment = new CouncilAssignment
        {
            Id = _assignmentId,
            CouncilId = _councilId,
            GroupId = _groupId,
            OrderNumber = 1
        };
        _councilRepo.Assignments.Add(_assignment);

        // Batch criteria
        _councilRepo.GradingCriteriaList.AddRange([
            new GradingCriteria { Id = _crit1Id, BatchId = _batchId, CriteriaCode = "C1", CriteriaName = "Nội dung & Kết quả", MaxScore = 6.0m, OrderIndex = 1 },
            new GradingCriteria { Id = _crit2Id, BatchId = _batchId, CriteriaCode = "C2", CriteriaName = "Thuyết trình & Trả lời", MaxScore = 4.0m, OrderIndex = 2 }
        ]);

        // Users
        _userInfoReader.Users[_chairmanUserId] = new CouncilUserInfoContractDto(_chairmanUserId, "GS. Chủ Tịch", "chutich@uit.edu.vn", "GV01", true);
        _userInfoReader.Users[_secretaryUserId] = new CouncilUserInfoContractDto(_secretaryUserId, "TS. Thư Ký", "thuky@uit.edu.vn", "GV02", true);
        _userInfoReader.Users[_memberUserId] = new CouncilUserInfoContractDto(_memberUserId, "ThS. Ủy Viên", "uyvien@uit.edu.vn", "GV03", true);
        _userInfoReader.Users[_reviewerUserId] = new CouncilUserInfoContractDto(_reviewerUserId, "TS. Phản Biện", "phanbien@uit.edu.vn", "GV04", true);
    }

    private SubmitCouncilScoreHandler CreateSubmitHandler() =>
        new(_councilRepo, _uow, _currentUser, new TestCouncilScoreWriteGuard(), TimeProvider.System);

    private CheckCouncilDiscrepancyHandler CreateCheckHandler() =>
        new(_councilRepo, _userInfoReader, _currentUser, new TestScoreAuthority(_currentUser));

    // =========================================================================
    // 1. SubmitCouncilScoreHandler Tests
    // =========================================================================

    [Fact]
    public async Task SubmitScore_WhenUserNotAuthenticated_ReturnsForbidden()
    {
        _currentUser.IsAuthenticated = false;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.Forbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenAssignmentNotFound_ReturnsNotFound()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            Guid.NewGuid(),
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.AssignmentNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenCouncilIsNotActive_ReturnsConflict()
    {
        _council.Status = CouncilStatus.Draft;
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotActiveForScoring.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenUserNotCouncilMember_ReturnsForbidden()
    {
        _currentUser.UserId = Guid.NewGuid(); // Outside lecturer
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.UserNotCouncilMember.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenMemberIsActiveReviewer_TH1_BR07_ReturnsConflict()
    {
        // GVPB kiêm Ủy viên hội đồng: _memberUserId is both CouncilMember and Reviewer
        _councilRepo.ActiveReviewerAssignments[_groupId] = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('a', 64), Guid.NewGuid(), DateTime.UtcNow);

        _currentUser.UserId = _memberUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ReviewerCouncilMemberCannotScoreTwice.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenMinuteLocked_ReturnsConflict()
    {
        _councilRepo.Minutes.Add(new CouncilMinute
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = _assignmentId,
            LockStatus = CouncilMinuteLockStatus.Locked
        });

        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MinuteLocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenDetailsEmpty_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(_assignmentId, []));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.CriteriaRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenCriterionNotFound_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();
        var unknownCritId = Guid.NewGuid();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(unknownCritId, 5.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.CriteriaNotFound(unknownCritId).Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreExceedsMaxScore_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // MaxScore of crit1 is 6.0m; submit 6.5m
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 6.5m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ScoreExceedsMaxScore("C1", 6.5m, 6.0m).Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreNegative_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, -1.0m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.CriteriaScoreNegative("C1").Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenMissingRequiredCriterion_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Submit only crit1, missing crit2
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.MissingRequiredCriteria("C2").Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_ValidInput_SavesScoreAndEvaluationDetails()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.5m), new CriterionScoreInputDto(_crit2Id, 3.5m)],
            Feedback: "Sinh viên bảo vệ tự tin, trả lời tốt câu hỏi."));

        Assert.True(result.IsSuccess);
        Assert.Equal(9.0m, result.Value.Score);
        Assert.Equal("Sinh viên bảo vệ tự tin, trả lời tốt câu hỏi.", result.Value.Feedback);
        Assert.Equal(2, result.Value.Details.Count);

        var savedScore = _councilRepo.Scores.FirstOrDefault(s => s.CouncilMemberId == _chairmanMember.Id);
        Assert.NotNull(savedScore);
        Assert.Equal(9.0m, savedScore.Score);

        var savedDetails = _councilRepo.EvaluationDetails.Where(d => d.EvaluationId == savedScore.Id).ToList();
        Assert.Equal(2, savedDetails.Count);
        Assert.Contains(savedDetails, d => d.CriteriaId == _crit1Id && d.Score == 5.5m);
        Assert.Contains(savedDetails, d => d.CriteriaId == _crit2Id && d.Score == 3.5m);
    }

    [Fact]
    public async Task SubmitScore_Rescore_UpdatesExistingScoreAndDetails()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // First score: 5.0 + 3.0 = 8.0
        await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        // Rescore: 5.5 + 3.5 = 9.0
        var rescoreResult = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.5m), new CriterionScoreInputDto(_crit2Id, 3.5m)],
            Feedback: "Điểm đã cập nhật"));

        Assert.True(rescoreResult.IsSuccess);
        Assert.Equal(9.0m, rescoreResult.Value.Score);

        // Only 1 score row should exist for chairman
        var chairmanScores = _councilRepo.Scores.Where(s => s.CouncilMemberId == _chairmanMember.Id).ToList();
        Assert.Single(chairmanScores);
        Assert.Equal(9.0m, chairmanScores[0].Score);

        // Only 2 details should exist (old ones replaced)
        var details = _councilRepo.EvaluationDetails.Where(d => d.EvaluationId == chairmanScores[0].Id).ToList();
        Assert.Equal(2, details.Count);
    }

    [Fact]
    public async Task SubmitScore_WhenAllMembersScored_UpdatesMinuteStatusWithoutWritingScoreSummary()
    {
        var handler = CreateSubmitHandler();

        // Setup Advisor & Reviewer scores for the group
        _councilRepo.AdvisorEvaluations[_groupId] = CreateFakeAdvisorEvaluation(_groupId, 8.0m);
        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _reviewerUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('b', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revEval = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 8.5m, "Good", DefenseApprovalStatus.Approved, _reviewerUserId, DateTime.UtcNow);
        revEval.TrySubmit(8.5m, "Good", DefenseApprovalStatus.Approved, _reviewerUserId, DateTime.UtcNow);
        typeof(ReviewerAssignment).GetProperty("Evaluation")!.SetValue(revAssignment, revEval);
        _councilRepo.ActiveReviewerAssignments[_groupId] = revAssignment;

        // Chairman scores 9.0
        _currentUser.UserId = _chairmanUserId;
        await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.5m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        // Secretary scores 8.5
        _currentUser.UserId = _secretaryUserId;
        await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        // Member scores 8.0 (final member)
        _currentUser.UserId = _memberUserId;
        var finalResult = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(finalResult.IsSuccess);

        // Minute should be updated to AllScoresEntered
        var minute = _councilRepo.Minutes.FirstOrDefault(m => m.CouncilAssignmentId == _assignmentId);
        Assert.NotNull(minute);
        Assert.Equal(CouncilMinuteLockStatus.AllScoresEntered, minute.LockStatus);

        Assert.Empty(_councilRepo.ScoreSummaries);
        Assert.Empty(_councilRepo.ScoreSummaryComponents);
    }

    // =========================================================================
    // 2. CheckCouncilDiscrepancyHandler Tests
    // =========================================================================

    [Fact]
    public async Task CheckDiscrepancy_WhenAssignmentNotFound_ReturnsNotFound()
    {
        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.AssignmentNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CheckDiscrepancy_NoScores_ReturnsNoDiscrepancy()
    {
        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDiscrepancyDetected);
        Assert.Null(result.Value.ScoreDifference);
        Assert.False(result.Value.AllScoresSubmitted);
    }

    [Fact]
    public async Task CheckDiscrepancy_SmallDifference_LessThanTwo_ReturnsFalse()
    {
        // Chairman 8.5, Secretary 7.5 -> difference = 1.0 < 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 8.5m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.5m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDiscrepancyDetected);
        Assert.Equal(1.0m, result.Value.ScoreDifference);
        Assert.Equal(8.5m, result.Value.MaxScore);
        Assert.Equal(7.5m, result.Value.MinScore);
        Assert.Null(result.Value.WarningMessage);
    }

    [Fact]
    public async Task CheckDiscrepancy_DifferenceExactlyTwo_TriggersWarning()
    {
        // Chairman 9.0, Secretary 7.0 -> difference = 2.0 >= 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.0m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.0m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(2.0m, result.Value.ScoreDifference);
        Assert.NotNull(result.Value.WarningMessage);
        Assert.Contains("2.00", result.Value.WarningMessage);
    }

    [Fact]
    public async Task CheckDiscrepancy_DifferenceGreaterThanTwo_TriggersWarning()
    {
        // Chairman 9.5, Member 6.5 -> difference = 3.0 >= 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.5m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _regularMember.Id, Score = 6.5m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(3.0m, result.Value.ScoreDifference);
        Assert.NotNull(result.Value.WarningMessage);
        Assert.Contains("3.00", result.Value.WarningMessage);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenGVPBIsCouncilMember_IncludesReviewerScoreInDiscrepancy()
    {
        // GVPB is _regularMember. ReviewerEvaluation score is 6.5. Chairman is 8.8.
        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('c', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revEval = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, 6.5m, "GVPB feedback", DefenseApprovalStatus.Approved, _memberUserId, DateTime.UtcNow);
        revEval.TrySubmit(6.5m, "GVPB feedback", DefenseApprovalStatus.Approved, _memberUserId, DateTime.UtcNow);
        typeof(ReviewerAssignment).GetProperty("Evaluation")!.SetValue(revAssignment, revEval);
        _councilRepo.ActiveReviewerAssignments[_groupId] = revAssignment;

        // Chairman scored 8.8
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = _assignmentId,
            CouncilMemberId = _chairmanMember.Id,
            Score = 8.8m
        });

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        // Difference: 8.8 - 6.5 = 2.3 >= 2.0 -> Discrepancy warning should be triggered!
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(2.3m, result.Value.ScoreDifference);
        Assert.Equal(8.8m, result.Value.MaxScore);
        Assert.Equal(6.5m, result.Value.MinScore);

        var reviewerMemberDetail = result.Value.MemberScores.FirstOrDefault(m => m.UserId == _memberUserId);
        Assert.NotNull(reviewerMemberDetail);
        Assert.True(reviewerMemberDetail.IsReviewerInCouncil);
        Assert.True(reviewerMemberDetail.IsBlockedFromScoring);
        Assert.Equal(6.5m, reviewerMemberDetail.Score);
    }

    // =========================================================================
    // 3. Edge Cases & Boundary Value Tests
    // =========================================================================

    [Fact]
    public async Task SubmitScore_WhenDuplicateCriteriaInRequest_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Crit1 repeated twice
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [
                new CriterionScoreInputDto(_crit1Id, 3.0m),
                new CriterionScoreInputDto(_crit1Id, 2.0m),
                new CriterionScoreInputDto(_crit2Id, 3.0m)
            ]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.DuplicateCriteriaInRequest.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreIsExactlyZero_IsValidBoundaryCase()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Exact boundary: 0.0m for both criteria
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 0.0m), new CriterionScoreInputDto(_crit2Id, 0.0m)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(0.0m, result.Value.Score);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreIsExactlyMaxScore_IsValidBoundaryCase()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Exact boundary: 6.0m for crit1 and 4.0m for crit2 -> total 10.0m
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 6.0m), new CriterionScoreInputDto(_crit2Id, 4.0m)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(10.0m, result.Value.Score);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreIsSlightlyAboveMaxScore_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Boundary: 6.01m > 6.00m
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 6.01m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.ScoreExceedsMaxScore("C1", 6.01m, 6.0m).Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenScoreIsSlightlyBelowZero_ReturnsValidationError()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        // Boundary: -0.01m < 0.0m
        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, -0.01m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.CriteriaScoreNegative("C1").Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenCouncilIsCompleted_ReturnsConflict()
    {
        _council.Status = CouncilStatus.Completed;
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.0m)]));

        Assert.True(result.IsFailure);
        Assert.Equal(CouncilErrors.NotActiveForScoring.Code, result.Error.Code);
    }

    [Fact]
    public async Task SubmitScore_WhenMemberIsReviewerForAnotherGroup_CanScoreThisGroupSuccessfully()
    {
        var otherGroupId = Guid.NewGuid();
        // Member is reviewer for otherGroupId, NOT for _groupId!
        _councilRepo.ActiveReviewerAssignments[otherGroupId] = ReviewerAssignment.Create(
            Guid.NewGuid(), otherGroupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('d', 64), Guid.NewGuid(), DateTime.UtcNow);

        _currentUser.UserId = _memberUserId;
        var handler = CreateSubmitHandler();

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.5m)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(8.5m, result.Value.Score);
    }

    [Fact]
    public async Task SubmitScore_WithLongFeedback_PersistsSuccessfully()
    {
        _currentUser.UserId = _chairmanUserId;
        var handler = CreateSubmitHandler();
        var longFeedback = new string('A', 800);

        var result = await handler.HandleAsync(new SubmitCouncilScoreCommand(
            _assignmentId,
            [new CriterionScoreInputDto(_crit1Id, 5.0m), new CriterionScoreInputDto(_crit2Id, 3.0m)],
            Feedback: longFeedback));

        Assert.True(result.IsSuccess);
        Assert.Equal(longFeedback, result.Value.Feedback);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenDifferenceIsOnePointNineNine_ReturnsFalse()
    {
        // 8.99 and 7.00 -> diff = 1.99 < 2.0 -> no discrepancy!
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 8.99m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.00m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDiscrepancyDetected);
        Assert.Equal(1.99m, result.Value.ScoreDifference);
        Assert.Null(result.Value.WarningMessage);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenDifferenceIsTwoPointZeroZero_ReturnsTrue()
    {
        // 9.00 and 7.00 -> diff = 2.00 >= 2.0 -> discrepancy!
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.00m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.00m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(2.00m, result.Value.ScoreDifference);
        Assert.NotNull(result.Value.WarningMessage);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenDifferenceIsTwoPointZeroOne_ReturnsTrue()
    {
        // 9.01 and 7.00 -> diff = 2.01 >= 2.0 -> discrepancy!
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.01m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 7.00m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(2.01m, result.Value.ScoreDifference);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenAllMembersGiveSameScore_DifferenceIsZero()
    {
        // All members score 8.50 -> diff = 0.00
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 8.50m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 8.50m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _regularMember.Id, Score = 8.50m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsDiscrepancyDetected);
        Assert.Equal(0.00m, result.Value.ScoreDifference);
        Assert.Equal(8.50m, result.Value.AverageCouncilScore);
    }

    [Fact]
    public async Task CheckDiscrepancy_ExtremeScoresZeroAndTen_TriggersWarning()
    {
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 10.0m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 0.0m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(10.0m, result.Value.ScoreDifference);
    }

    [Fact]
    public async Task CheckDiscrepancy_ThreeMembers_TwoAreFarApart_TriggersWarning()
    {
        // 7.0, 8.0, 9.0 -> max 9.0, min 7.0 -> diff 2.0 >= 2.0
        _councilRepo.Scores.AddRange([
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _chairmanMember.Id, Score = 9.0m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _secretaryMember.Id, Score = 8.0m },
            new CouncilScore { Id = Guid.NewGuid(), CouncilAssignmentId = _assignmentId, CouncilMemberId = _regularMember.Id, Score = 7.0m }
        ]);

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsDiscrepancyDetected);
        Assert.Equal(2.0m, result.Value.ScoreDifference);
        Assert.Equal(8.0m, result.Value.AverageCouncilScore);
    }

    [Fact]
    public async Task CheckDiscrepancy_WhenGVPBHasNoSubmittedScoreYet_DoesNotCrashAndIgnoresNullScore()
    {
        // GVPB is council member, but ReviewerEvaluation is still draft with score = null
        var revAssignment = ReviewerAssignment.Create(
            Guid.NewGuid(), _groupId, _memberUserId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, Guid.NewGuid(), new string('e', 64), Guid.NewGuid(), DateTime.UtcNow);
        var revEval = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), revAssignment.Id, null, null, null, _memberUserId, DateTime.UtcNow);
        typeof(ReviewerAssignment).GetProperty("Evaluation")!.SetValue(revAssignment, revEval);
        _councilRepo.ActiveReviewerAssignments[_groupId] = revAssignment;

        // Chairman scored 8.5
        _councilRepo.Scores.Add(new CouncilScore
        {
            Id = Guid.NewGuid(),
            CouncilAssignmentId = _assignmentId,
            CouncilMemberId = _chairmanMember.Id,
            Score = 8.5m
        });

        var handler = CreateCheckHandler();
        var result = await handler.HandleAsync(new CheckCouncilDiscrepancyQuery(_assignmentId));

        Assert.True(result.IsSuccess);
        // Only 1 valid score (8.5m), reviewer score is null -> diff = 0, isDiscrepancy = false
        Assert.False(result.Value.IsDiscrepancyDetected);
        Assert.Equal(0m, result.Value.ScoreDifference);
    }

    private static AdvisorEvaluation CreateFakeAdvisorEvaluation(Guid groupId, decimal score)
    {
        var evalType = typeof(AdvisorEvaluation);
        var ctor = evalType.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .First(c => c.GetParameters().Length == 10);

        return (AdvisorEvaluation)ctor.Invoke([
            Guid.NewGuid(), groupId, Guid.NewGuid(), 1, Guid.NewGuid(),
            new string('0', 64), score, "Tot", Guid.NewGuid(), DateTime.UtcNow
        ]);
    }
}

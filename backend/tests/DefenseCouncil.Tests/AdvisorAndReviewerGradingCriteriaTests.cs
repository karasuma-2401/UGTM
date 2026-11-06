using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.GetAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class AdvisorAndReviewerGradingCriteriaTests
{
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly Guid _topicId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _finalReportId = Guid.NewGuid();
    private readonly Guid _submissionFileId = Guid.NewGuid();
    private readonly Guid _advisorId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _assignmentId = Guid.NewGuid();
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly Guid _crit1Id = Guid.NewGuid();
    private readonly Guid _crit2Id = Guid.NewGuid();
    private readonly List<GradingCriteria> _criteria;

    public AdvisorAndReviewerGradingCriteriaTests()
    {
        _criteria =
        [
            new GradingCriteria { Id = _crit1Id, BatchId = _batchId, CriteriaCode = "C1", CriteriaName = "Nội dung & Kết quả", MaxScore = 6.0m, OrderIndex = 1 },
            new GradingCriteria { Id = _crit2Id, BatchId = _batchId, CriteriaCode = "C2", CriteriaName = "Hình thức & Báo cáo", MaxScore = 4.0m, OrderIndex = 2 }
        ];
    }

    // =========================================================================
    // 1. Advisor Evaluation (GVHD) Tests
    // =========================================================================

    [Fact]
    public async Task SaveAdvisorEvaluation_WithValidDetails_ComputesScoreAndPersistsDetails()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành tốt",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.5m),
                new CriterionScoreInputDto(_crit2Id, 3.5m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(9.0m, result.Value.Score);
        Assert.NotNull(result.Value.Details);
        Assert.Equal(2, result.Value.Details.Count);
        Assert.Equal(5.5m, result.Value.Details.First(d => d.CriteriaId == _crit1Id).Score);
        Assert.Equal(3.5m, result.Value.Details.First(d => d.CriteriaId == _crit2Id).Score);

        // Verify persisted details
        var savedDetails = await repo.GetEvaluationDetailsByEvaluationIdAsync(result.Value.Id);
        Assert.Equal(2, savedDetails.Count);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithDuplicateCriteria_ReturnsDuplicateCriteriaError()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 3.0m),
                new CriterionScoreInputDto(_crit1Id, 2.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("AdvisorEvaluation.DuplicateCriteria", result.Error.Code);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithUnknownCriteria_ReturnsCriteriaNotFoundError()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var unknownId = Guid.NewGuid();
        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(unknownId, 3.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("AdvisorEvaluation.CriteriaNotFound", result.Error.Code);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithNegativeScore_ReturnsCriteriaScoreNegativeError()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, -0.5m),
                new CriterionScoreInputDto(_crit2Id, 3.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("AdvisorEvaluation.CriteriaScoreNegative", result.Error.Code);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithScoreExceedingMax_ReturnsScoreExceedsMaxScoreError()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 6.5m), // Max is 6.0
                new CriterionScoreInputDto(_crit2Id, 3.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("AdvisorEvaluation.ScoreExceedsMaxScore", result.Error.Code);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithMissingRequiredCriteria_ReturnsMissingRequiredCriteriaError()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            null,
            "Hoàn thành",
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.0m)
                // Missing _crit2Id
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("AdvisorEvaluation.MissingRequiredCriteria", result.Error.Code);
    }

    [Fact]
    public async Task SaveAdvisorEvaluation_WithoutDetails_UsesFallbackScore()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateAdvisorHandler(repo);
        var command = new SaveAdvisorEvaluationCommand(
            _finalReportId,
            1,
            null,
            8.25m,
            "Chấm trực tiếp",
            Guid.NewGuid());

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(8.25m, result.Value.Score);
    }

    [Fact]
    public async Task GetAdvisorEvaluation_ReturnsDetails()
    {
        var repo = new FakeAdvisorEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var evaluation = AdvisorEvaluation.Create(
            Guid.NewGuid(), _groupId, _finalReportId, 1, _submissionFileId, Checksum, 8.5m, "Tốt", _advisorId, DateTime.UtcNow);
        repo.Add(evaluation);

        await repo.SaveEvaluationDetailsAsync(evaluation.Id, [
            new EvaluationDetail { Id = Guid.NewGuid(), CriteriaId = _crit1Id, Criteria = _criteria[0], Score = 5.0m },
            new EvaluationDetail { Id = Guid.NewGuid(), CriteriaId = _crit2Id, Criteria = _criteria[1], Score = 3.5m }
        ]);

        var workflow = new FakeAdvisorWorkflow();
        workflow.GroupContext = new AdvisorEvaluationGroupContextContractDto(_groupId, _topicId);
        var assignmentContract = new FakeAdvisorAssignmentContract { Assignment = new TopicAdvisorAssignmentContractDto(_topicId, _advisorId, _batchId) };
        var currentUser = new FakeCurrentUser { UserId = _advisorId, IsAuthenticated = true, Roles = new HashSet<string> { "Lecturer" } };

        var handler = new GetAdvisorEvaluationHandler(repo, workflow, assignmentContract, currentUser);
        var result = await handler.HandleAsync(_groupId);

        Assert.True(result.IsSuccess);
        Assert.Equal(8.5m, result.Value.Score);
        Assert.NotNull(result.Value.Details);
        Assert.Equal(2, result.Value.Details.Count);
    }

    // =========================================================================
    // 2. Reviewer Evaluation (GVPB) Tests
    // =========================================================================

    [Fact]
    public async Task SaveReviewerEvaluation_SaveDraft_WithPartialDetails_Succeeds()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.SaveDraft,
            1,
            null,
            null,
            "Nháp phần 1",
            null,
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(5.0m, result.Value.Evaluation.Score);
        Assert.Equal("Draft", result.Value.Evaluation.Status);
        Assert.NotNull(result.Value.Evaluation.Details);
        Assert.Single(result.Value.Evaluation.Details);
    }

    [Fact]
    public async Task SaveReviewerEvaluation_Submit_WithMissingCriteria_ReturnsMissingRequiredCriteriaError()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.Submit,
            1,
            null,
            null,
            "Chốt đánh giá",
            DefenseApprovalStatus.Approved,
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.0m)
                // Missing _crit2Id
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("ReviewerEvaluation.MissingRequiredCriteria", result.Error.Code);
    }

    [Fact]
    public async Task SaveReviewerEvaluation_Submit_WithAllCriteria_ComputesTotalScoreAndSubmits()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.Submit,
            1,
            null,
            null,
            "Chốt đánh giá chuẩn xác",
            DefenseApprovalStatus.Approved,
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.5m),
                new CriterionScoreInputDto(_crit2Id, 3.8m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(9.3m, result.Value.Evaluation.Score);
        Assert.Equal("Submitted", result.Value.Evaluation.Status);
        Assert.NotNull(result.Value.Evaluation.Details);
        Assert.Equal(2, result.Value.Evaluation.Details.Count);
        Assert.True(result.Value.Evaluation.IsLocked);

        var savedDetails = await repo.GetEvaluationDetailsByEvaluationIdAsync(result.Value.Evaluation.Id);
        Assert.Equal(2, savedDetails.Count);
    }

    [Fact]
    public async Task SaveReviewerEvaluation_Submit_WithScoreExceedingMax_ReturnsScoreExceedsMaxScoreError()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.Submit,
            1,
            null,
            null,
            "Chốt",
            DefenseApprovalStatus.Approved,
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 6.5m), // Max is 6.0
                new CriterionScoreInputDto(_crit2Id, 3.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("ReviewerEvaluation.ScoreExceedsMaxScore", result.Error.Code);
    }

    [Fact]
    public async Task SaveReviewerEvaluation_Submit_WithDuplicateCriteria_ReturnsDuplicateCriteriaError()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.Submit,
            1,
            null,
            null,
            "Chốt",
            DefenseApprovalStatus.Approved,
            Guid.NewGuid(),
            [
                new CriterionScoreInputDto(_crit1Id, 5.0m),
                new CriterionScoreInputDto(_crit1Id, 4.0m)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal("ReviewerEvaluation.DuplicateCriteria", result.Error.Code);
    }

    [Fact]
    public async Task SaveReviewerEvaluation_Submit_WithoutDetails_UsesFallbackScore()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var handler = CreateReviewerHandler(repo);
        var command = new SaveReviewerEvaluationCommand(
            _assignmentId,
            ReviewerEvaluationAction.Submit,
            1,
            null,
            8.0m,
            "Chấm trực tiếp không barem",
            DefenseApprovalStatus.Approved,
            Guid.NewGuid());

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(8.0m, result.Value.Evaluation.Score);
        Assert.Equal("Submitted", result.Value.Evaluation.Status);
    }

    [Fact]
    public async Task GetReviewerEvaluation_ReturnsDetails()
    {
        var repo = new FakeReviewerEvaluationRepo();
        repo.CriteriaList.AddRange(_criteria);

        var assignment = ReviewerAssignment.Create(
            _assignmentId, _groupId, _reviewerId, _advisorId, Guid.NewGuid(), _finalReportId, 1, _submissionFileId, Checksum, Guid.NewGuid(), DateTime.UtcNow);
        var assignmentRepo = new FakeReviewerAssignmentRepo { Assignment = assignment };

        var evaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), _assignmentId, 8.5m, "Góp ý tốt", DefenseApprovalStatus.Approved, _reviewerId, DateTime.UtcNow);
        repo.Add(evaluation);

        await repo.SaveEvaluationDetailsAsync(evaluation.Id, [
            new EvaluationDetail { Id = Guid.NewGuid(), CriteriaId = _crit1Id, Criteria = _criteria[0], Score = 5.0m },
            new EvaluationDetail { Id = Guid.NewGuid(), CriteriaId = _crit2Id, Criteria = _criteria[1], Score = 3.5m }
        ]);

        var groups = new FakeReviewerGroupReader { Group = new ReviewerFeedbackGroupContractDto(_groupId, _topicId, _batchId, true, true) };
        var topics = new FakeReviewerTopicReader { Topic = new ReviewerAssignmentTopicContractDto(_topicId, _batchId, _advisorId, "Đề tài", null) };
        var currentUser = new FakeCurrentUser { UserId = _reviewerId, IsAuthenticated = true, Roles = new HashSet<string> { "Lecturer" }, Permissions = new HashSet<string> { "thesis.reviewer-evaluations.read" } };
        var auth = new ReviewerEvaluationAuthorization(currentUser, new FakeReviewerCandidateReader());

        var handler = new GetReviewerEvaluationHandler(assignmentRepo, repo, auth, groups, topics, currentUser);
        var result = await handler.HandleAsync(_assignmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(8.5m, result.Value.Score);
        Assert.NotNull(result.Value.Details);
        Assert.Equal(2, result.Value.Details.Count);
    }

    // =========================================================================
    // Test Helpers & Fakes
    // =========================================================================

    private SaveAdvisorEvaluationHandler CreateAdvisorHandler(FakeAdvisorEvaluationRepo repo)
    {
        var idempotencyRepo = new FakeAdvisorIdempotencyRepo();
        var workflow = new FakeAdvisorWorkflow
        {
            FinalReportContext = new AdvisorEvaluationFinalReportContextContractDto(
                _finalReportId, _groupId, _topicId, true, true, true, true, 1, _submissionFileId, Checksum),
            LockedReport = new AdvisorEvaluationLockedFinalReportContractDto(
                _finalReportId, _groupId, 1, _submissionFileId, Checksum)
        };
        var assignmentContract = new FakeAdvisorAssignmentContract
        {
            Assignment = new TopicAdvisorAssignmentContractDto(_topicId, _advisorId, _batchId)
        };
        var coordinator = new FakeAdvisorTransactionCoordinator();
        var auditOutbox = new FakeDefenseCouncilAuditOutbox();
        var currentUser = new FakeCurrentUser { UserId = _advisorId, IsAuthenticated = true, Roles = new HashSet<string> { "Lecturer" } };

        return new SaveAdvisorEvaluationHandler(
            repo, idempotencyRepo, workflow, assignmentContract, coordinator, auditOutbox, currentUser, TimeProvider.System);
    }

    private SaveReviewerEvaluationHandler CreateReviewerHandler(FakeReviewerEvaluationRepo repo)
    {
        var assignment = ReviewerAssignment.Create(
            _assignmentId, _groupId, _reviewerId, _advisorId, Guid.NewGuid(), _finalReportId, 1, _submissionFileId, Checksum, Guid.NewGuid(), DateTime.UtcNow);
        Set(assignment, "Version", 1u);
        var assignmentRepo = new FakeReviewerAssignmentRepo { Assignment = assignment };

        var workflow = new FakeReviewerWorkflow
        {
            LockResult = new ReviewerAssignmentReportAccessContractDto(
                assignment.Id, assignment.Version, assignment.ReviewerId,
                new ReviewerAssignmentBindingContractDto(_groupId, _topicId, _batchId, _advisorId, Guid.NewGuid(), _finalReportId, 1, _submissionFileId, Checksum, DateTime.UtcNow))
        };
        var coordinator = new FakeReviewerTransactionCoordinator();
        var receipts = new FakeReviewerReceiptsRepo();
        var batches = new FakeReviewerBatchReader { Batch = new ReviewerAssignmentBatchContractDto(_batchId, true) };
        var currentUser = new FakeCurrentUser { UserId = _reviewerId, IsAuthenticated = true, Roles = new HashSet<string> { "Lecturer" } };
        var auth = new ReviewerEvaluationAuthorization(currentUser, new FakeReviewerCandidateReader());
        var audit = new ReviewerEvaluationAudit(new FakeDefenseCouncilAuditOutbox(), currentUser);

        return new SaveReviewerEvaluationHandler(
            workflow, coordinator, assignmentRepo, repo, receipts, batches, auth, audit, currentUser, TimeProvider.System);
    }

    private static void Set(object target, string property, object? value)
        => target.GetType().GetProperty(property)!.SetValue(target, value);

    private sealed class FakeAdvisorEvaluationRepo : IAdvisorEvaluationRepository
    {
        public readonly Dictionary<Guid, AdvisorEvaluation> EvaluationsById = new();
        public readonly Dictionary<Guid, AdvisorEvaluation> EvaluationsByGroupId = new();
        public readonly List<GradingCriteria> CriteriaList = new();
        public readonly Dictionary<Guid, List<EvaluationDetail>> DetailsByEvaluationId = new();

        public Task<AdvisorEvaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(EvaluationsById.GetValueOrDefault(id));

        public Task<AdvisorEvaluation?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(EvaluationsByGroupId.GetValueOrDefault(groupId));

        public void Add(AdvisorEvaluation evaluation)
        {
            EvaluationsById[evaluation.Id] = evaluation;
            EvaluationsByGroupId[evaluation.GroupId] = evaluation;
        }

        public Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GradingCriteria>>(CriteriaList.Where(c => c.BatchId == batchId).OrderBy(c => c.OrderIndex).ToList());

        public Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationDetail>>(DetailsByEvaluationId.GetValueOrDefault(evaluationId, []));

        public Task SaveEvaluationDetailsAsync(Guid evaluationId, IEnumerable<EvaluationDetail> details, CancellationToken cancellationToken = default)
        {
            DetailsByEvaluationId[evaluationId] = details.Select(d => { d.EvaluationId = evaluationId; return d; }).ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdvisorIdempotencyRepo : IAdvisorEvaluationIdempotencyRepository
    {
        public readonly Dictionary<(Guid Actor, string Operation, Guid Key), AdvisorEvaluationIdempotencyRecord> Records = new();

        public Task<AdvisorEvaluationIdempotencyRecord?> GetAsync(Guid actorId, string operation, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.GetValueOrDefault((actorId, operation, idempotencyKey)));

        public void Add(AdvisorEvaluationIdempotencyRecord record)
            => Records[(record.ActorId, record.Operation, record.IdempotencyKey)] = record;
    }

    private sealed class FakeAdvisorWorkflow : IAdvisorEvaluationWorkflowContract
    {
        public AdvisorEvaluationFinalReportContextContractDto? FinalReportContext { get; set; }
        public AdvisorEvaluationLockedFinalReportContractDto? LockedReport { get; set; }
        public AdvisorEvaluationGroupContextContractDto? GroupContext { get; set; }

        public Task<AdvisorEvaluationFinalReportContextContractDto?> GetFinalReportContextAsync(Guid finalReportId, CancellationToken cancellationToken = default)
            => Task.FromResult(FinalReportContext);

        public Task<AdvisorEvaluationGroupContextContractDto?> GetGroupContextAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(GroupContext);

        public Task<AdvisorEvaluationLockedFinalReportContractDto?> GetLockedInitialFinalReportAsync(Guid finalReportId, CancellationToken cancellationToken = default)
            => Task.FromResult(LockedReport);

        public Task<AdvisorEvaluationGroupReadinessResultContractDto> TryMakeGroupReadyForReviewerAssignmentAsync(Guid groupId, Guid finalReportId, int expectedReportVersionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(new AdvisorEvaluationGroupReadinessResultContractDto(AdvisorEvaluationGroupReadinessOutcome.Succeeded, groupId));

        public void AddDecisionAudit(AdvisorEvaluationDecisionAuditContractDto decision) { }

        public Task<bool> IsGroupReadyForReviewerAssignmentAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeAdvisorAssignmentContract : ITopicAdvisorAssignmentContract
    {
        public TopicAdvisorAssignmentContractDto? Assignment { get; set; }

        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignment);
    }

    private sealed class FakeAdvisorTransactionCoordinator : IAdvisorWorkflowTransactionCoordinator
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> workflow, CancellationToken cancellationToken = default)
            => workflow(cancellationToken);

        public async Task<TResult> ExecuteWithPostSaveAsync<TResult>(
            Func<CancellationToken, Task<TResult>> workflow,
            Func<TResult, CancellationToken, Task<TResult>> postSave,
            CancellationToken cancellationToken = default)
        {
            var result = await workflow(cancellationToken);
            return await postSave(result, cancellationToken);
        }
    }

    private sealed class FakeReviewerEvaluationRepo : IReviewerEvaluationRepository
    {
        public readonly Dictionary<Guid, ReviewerEvaluation> EvaluationsByAssignmentId = new();
        public readonly List<GradingCriteria> CriteriaList = new();
        public readonly Dictionary<Guid, List<EvaluationDetail>> DetailsByEvaluationId = new();

        public Task<ReviewerEvaluation?> GetByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(EvaluationsByAssignmentId.GetValueOrDefault(assignmentId));

        public Task<ReviewerEvaluation?> LockByAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(EvaluationsByAssignmentId.GetValueOrDefault(assignmentId));

        public void Add(ReviewerEvaluation evaluation)
        {
            Set(evaluation, "Version", 1u);
            EvaluationsByAssignmentId[evaluation.AssignmentId] = evaluation;
        }

        public Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GradingCriteria>>(CriteriaList.Where(c => c.BatchId == batchId).OrderBy(c => c.OrderIndex).ToList());

        public Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EvaluationDetail>>(DetailsByEvaluationId.GetValueOrDefault(evaluationId, []));

        public Task SaveEvaluationDetailsAsync(Guid evaluationId, IEnumerable<EvaluationDetail> details, CancellationToken cancellationToken = default)
        {
            DetailsByEvaluationId[evaluationId] = details.Select(d => { d.EvaluationId = evaluationId; return d; }).ToList();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReviewerAssignmentRepo : IReviewerAssignmentRepository
    {
        public ReviewerAssignment? Assignment { get; set; }

        public Task<ReviewerAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignment?.Id == id ? Assignment : null);

        public Task<ReviewerAssignment?> GetActiveByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignment?.GroupId == groupId && Assignment.IsActive ? Assignment : null);

        public Task<IReadOnlyList<ReviewerAssignment>> GetActiveByReviewerAndFileAsync(Guid reviewerId, Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignment>>(Assignment != null && Assignment.ReviewerId == reviewerId && Assignment.SubmissionFileId == fileId && Assignment.IsActive ? [Assignment] : []);

        public Task<ReviewerAssignment?> LockByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignment?.Id == id ? Assignment : null);

        public Task<bool> HasEvaluationAsync(Guid assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Assignment?.Id == assignmentId && Assignment.Evaluation != null);

        public void Add(ReviewerAssignment assignment) => Assignment = assignment;
    }

    private sealed class FakeReviewerWorkflow : IReviewerAssignmentWorkflowContract
    {
        public ReviewerAssignmentReportAccessContractDto? LockResult { get; set; }

        public Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid groupId, Guid reviewerId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid groupId, Guid reviewerId, Guid expectedAdvisorEvaluationId, int expectedReportVersionNo, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReviewerAssignmentReportAccessContractDto?> GetActiveReportAccessAsync(Guid reviewerId, Guid fileId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReviewerAssignmentReportAccessContractDto?> LockForEvaluationAsync(Guid assignmentId, Guid reviewerId, CancellationToken cancellationToken = default)
            => Task.FromResult(LockResult);
    }

    private sealed class FakeReviewerTransactionCoordinator : IReviewerAssignmentTransactionCoordinator
    {
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow, CancellationToken cancellationToken = default)
        {
            var result = await workflow(cancellationToken);
            return result.Value;
        }

        public async Task<T> ExecuteWithPostSaveAsync<T>(
            Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
            Func<T, CancellationToken, Task<T>> postSave, CancellationToken cancellationToken = default)
        {
            var result = await workflow(cancellationToken);
            if (result.ShouldCommit)
            {
                if (result.Value is not null)
                {
                    var evalProp = result.Value.GetType().GetProperty("Evaluation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (evalProp?.GetValue(result.Value) is ReviewerEvaluation eval && eval.Version == 0)
                    {
                        Set(eval, "Version", 1u);
                    }
                }
                return await postSave(result.Value, cancellationToken);
            }
            return result.Value;
        }
    }

    private sealed class FakeReviewerReceiptsRepo : IReviewerEvaluationIdempotencyRepository
    {
        public readonly Dictionary<(Guid Actor, string Operation, Guid Key), ReviewerEvaluationIdempotencyRecord> Records = new();

        public Task LockRequestAsync(Guid actorId, string operation, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<ReviewerEvaluationIdempotencyRecord?> GetAsync(Guid actorId, string operation, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Records.GetValueOrDefault((actorId, operation, idempotencyKey)));

        public void Add(ReviewerEvaluationIdempotencyRecord record)
            => Records[(record.ActorId, record.Operation, record.IdempotencyKey)] = record;
    }

    private sealed class FakeReviewerBatchReader : IReviewerAssignmentBatchReader
    {
        public ReviewerAssignmentBatchContractDto? Batch { get; set; }

        public Task<IReadOnlyList<ReviewerAssignmentBatchContractDto>> GetManyAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentBatchContractDto>>(Batch != null && batchIds.Contains(Batch.BatchId) ? [Batch] : []);

        public Task<ReviewerAssignmentBatchContractDto?> GetAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(Batch?.BatchId == batchId ? Batch : null);

        public Task<ReviewerAssignmentBatchContractDto?> LockAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(Batch?.BatchId == batchId ? Batch : null);
    }

    private sealed class FakeReviewerGroupReader : IReviewerFeedbackGroupReader
    {
        public ReviewerFeedbackGroupContractDto? Group { get; set; }

        public Task<ReviewerFeedbackGroupContractDto?> GetAsync(Guid groupId, Guid actorId, CancellationToken cancellationToken = default)
            => Task.FromResult(Group);

        public Task<bool> IsReportSnapshotValidAsync(ReviewerFeedbackReportSnapshotContractDto snapshot, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeReviewerTopicReader : IReviewerAssignmentTopicReader
    {
        public ReviewerAssignmentTopicContractDto? Topic { get; set; }

        public Task<IReadOnlyList<ReviewerAssignmentTopicContractDto>> GetManyAsync(IReadOnlyCollection<Guid> topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentTopicContractDto>>(Topic != null && topicIds.Contains(Topic.TopicId) ? [Topic] : []);

        public Task<IReadOnlyList<Guid>> FindIdsAsync(Guid? batchId, string? keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Topic != null && (!batchId.HasValue || Topic.BatchId == batchId.Value) ? [Topic.TopicId] : []);

        public Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid topicId, CancellationToken cancellationToken = default)
            => Task.FromResult(Topic?.TopicId == topicId ? Topic : null);

        public Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid topicId, CancellationToken cancellationToken = default)
            => Task.FromResult(Topic?.TopicId == topicId ? Topic : null);
    }

    private sealed class FakeReviewerCandidateReader : IReviewerCandidateReader
    {
        public Task<IReadOnlyList<Guid>> FindUserIdsAsync(string keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<ReviewerAccountContractDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ReviewerAccountContractDto?>(new ReviewerAccountContractDto(userId, true, true));

        public Task<ReviewerAccountContractDto?> LockAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ReviewerAccountContractDto?>(new ReviewerAccountContractDto(userId, true, true));

        public Task<ReviewerCandidatePageContractDto> SearchAsync(Guid excludedAdvisorId, string? keyword, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReviewerCandidatePageContractDto([], 0, pageNumber, pageSize));
    }
}

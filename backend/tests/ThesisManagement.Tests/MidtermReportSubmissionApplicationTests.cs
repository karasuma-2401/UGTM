using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class MidtermReportSubmissionApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    [Fact]
    public async Task Member_can_resubmit_midterm_report_and_approval_returns_group_to_in_progress()
    {
        var group = CreateInProgressGroup();
        var fixtures = new WorkflowFixtures(group);

        var firstSubmission = await fixtures.CreateSubmitHandler(StudentUser())
            .HandleAsync(SubmitCommand(group.Id));
        var revisionReview = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewMidtermReportCommand(group.Id, "NeedRevision", "Bổ sung kết quả thực nghiệm."));
        var secondSubmission = await fixtures.CreateSubmitHandler(StudentUser())
            .HandleAsync(SubmitCommand(group.Id));
        var approval = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewMidtermReportCommand(group.Id, "Approved", null));

        Assert.True(firstSubmission.IsSuccess);
        Assert.True(revisionReview.IsSuccess);
        Assert.True(secondSubmission.IsSuccess);
        Assert.Equal(2, secondSubmission.Value.VersionNo);
        Assert.True(approval.IsSuccess);
        Assert.Equal(StudentGroupStatus.InProgress, group.Status);
        Assert.Equal(MidtermReportStatus.Approved, fixtures.Report!.Status);
        Assert.Collection(
            fixtures.Audit.Entries,
            entry => Assert.Equal(IntegrationAuditActions.MidtermReportSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.MidtermReportReviewed, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.MidtermReportSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.MidtermReportReviewed, entry.Action));

        var history = await fixtures.CreateHistoryHandler(StudentUser())
            .HandleAsync(new GetMidtermReportSubmissionHistoryQuery(group.Id));
        Assert.True(history.IsSuccess);
        Assert.Collection(
            history.Value.Versions,
            version => Assert.Equal(nameof(ReviewDecision.NeedRevision), version.Review!.Decision),
            version => Assert.Equal(nameof(ReviewDecision.Approved), version.Review!.Decision));

        var download = await fixtures.CreateDownloadHandler(StudentUser())
            .HandleAsync(new GetReviewGateSubmissionFileDownloadQuery(history.Value.Versions[0].File.Id));
        Assert.True(download.IsSuccess);
        Assert.Equal(history.Value.Versions[0].File.Id, download.Value.FileId);
    }

    [Fact]
    public async Task Rejection_requires_feedback_and_stops_group()
    {
        var group = CreateInProgressGroup();
        var fixtures = new WorkflowFixtures(group);
        await fixtures.CreateSubmitHandler(StudentUser()).HandleAsync(SubmitCommand(group.Id));

        var missingFeedback = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewMidtermReportCommand(group.Id, "Rejected", "  "));
        var rejection = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewMidtermReportCommand(group.Id, "Rejected", "Chưa đạt yêu cầu học thuật."));

        Assert.True(missingFeedback.IsFailure);
        Assert.Equal(MidtermReportSubmissionErrors.InvalidRequest.Code, missingFeedback.Error.Code);
        Assert.True(rejection.IsSuccess);
        Assert.Equal(StudentGroupStatus.Stopped, group.Status);
        Assert.Equal(MidtermReportStatus.Rejected, fixtures.Report!.Status);
    }

    [Fact]
    public async Task Student_cannot_review_midterm_report()
    {
        var group = CreateInProgressGroup();
        var fixtures = new WorkflowFixtures(group);
        await fixtures.CreateSubmitHandler(StudentUser()).HandleAsync(SubmitCommand(group.Id));

        var result = await fixtures.CreateReviewHandler(StudentUser())
            .HandleAsync(new ReviewMidtermReportCommand(group.Id, "Approved", null));

        Assert.True(result.IsFailure);
        Assert.Equal(MidtermReportSubmissionErrors.ReviewAccessDenied.Code, result.Error.Code);
        Assert.Equal(MidtermReportStatus.Submitted, fixtures.Report!.Status);
    }

    [Fact]
    public async Task Initial_submission_after_deadline_is_rejected_before_upload()
    {
        var group = CreateInProgressGroup();
        var fixtures = new WorkflowFixtures(group, now: Now.AddDays(2));

        var result = await fixtures.CreateSubmitHandler(StudentUser()).HandleAsync(SubmitCommand(group.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(MidtermReportSubmissionErrors.InitialSubmissionDeadlinePassed.Code, result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    private static StudentGroup CreateInProgressGroup()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1));
        group.AddMember(StudentId, BatchId, Now.AddDays(-1));
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        return group;
    }

    private static SubmitMidtermReportCommand SubmitCommand(Guid groupId)
        => new(groupId, "bao-cao-giua-ky.pdf", new MemoryStream("%PDF-1.7\ncontent"u8.ToArray()));

    private static ICurrentUser StudentUser() => new FakeCurrentUser(StudentId);

    private static ICurrentUser ReviewerUser()
        => new FakeCurrentUser(ReviewerId, permissions: [PermissionCodes.ThesisReviewGatesReview]);

    private sealed class WorkflowFixtures
    {
        private readonly FakeStudentGroupRepository _groups;
        private readonly FakeMidtermReportRepository _reports = new();
        private readonly FakeReviewGateConfigurationRepository _configurations;
        private readonly FakeSubmissionFileRepository _files = new();
        private readonly FakeSubmissionReviewRepository _reviews = new();
        private readonly FakeUnitOfWork _unitOfWork = new();

        public WorkflowFixtures(StudentGroup group, DateTime? now = null)
        {
            _groups = new FakeStudentGroupRepository(group);
            _configurations = new FakeReviewGateConfigurationRepository(CreateConfiguration());
            NowProvider = new FixedTimeProvider(now ?? Now);
        }

        public FakeObjectStorage ObjectStorage { get; } = new();
        public FakeAuditOutbox Audit { get; } = new();
        public TimeProvider NowProvider { get; }
        public MidtermReport? Report => _reports.Report;

        public SubmitMidtermReportHandler CreateSubmitHandler(ICurrentUser currentUser)
            => new(
                _groups,
                _reports,
                _configurations,
                _files,
                new ThesisSubmissionFileValidator(),
                new ThesisSubmissionObjectKeyFactory(),
                ObjectStorage,
                Audit,
                _unitOfWork,
                currentUser,
                NowProvider);

        public ReviewMidtermReportHandler CreateReviewHandler(ICurrentUser currentUser)
            => new(
                _groups,
                _reports,
                _configurations,
                _reviews,
                Audit,
                _unitOfWork,
                currentUser,
                NowProvider);

        public GetMidtermReportSubmissionHistoryHandler CreateHistoryHandler(ICurrentUser currentUser)
            => new(_groups, _reports, _files, _reviews, currentUser);

        public GetReviewGateSubmissionFileDownloadHandler CreateDownloadHandler(ICurrentUser currentUser)
            => new(_files, new FakeSyllabusRepository(), _reports, _groups, ObjectStorage, currentUser, NowProvider);

        private static ReviewGateConfiguration CreateConfiguration()
            => new(
                Guid.NewGuid(),
                BatchId,
                ReviewGateType.Midterm,
                Now.AddDays(1),
                Now.AddDays(2),
                Now.AddDays(3),
                ReviewerId,
                Now.AddDays(-1));
    }

    private sealed class FakeCurrentUser(
        Guid userId,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = (roles ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = (permissions ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FakeStudentGroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StudentGroup?>(id == group.Id ? group : null);

        public void Add(StudentGroup value) => throw new NotSupportedException();
    }

    private sealed class FakeMidtermReportRepository : IMidtermReportRepository
    {
        public MidtermReport? Report { get; private set; }

        public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Report?.Id == id ? Report : null);

        public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Report?.GroupId == groupId ? Report : null);

        public void Add(MidtermReport midtermReport) => Report = midtermReport;
    }

    private sealed class FakeSyllabusRepository : ISyllabusRepository
    {
        public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Syllabus?>(null);

        public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<Syllabus?>(null);

        public void Add(Syllabus syllabus) => throw new NotSupportedException();
    }

    private sealed class FakeReviewGateConfigurationRepository(ReviewGateConfiguration configuration)
        : IReviewGateConfigurationRepository
    {
        public Task<ReviewGateConfiguration?> GetByBatchAndGateAsync(
            Guid batchId,
            ReviewGateType gateType,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ReviewGateConfiguration?>(
                configuration.BatchId == batchId && configuration.GateType == gateType ? configuration : null);

        public Task<IReadOnlyList<ReviewGateConfiguration>> GetByBatchAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewGateConfiguration>>(
                configuration.BatchId == batchId ? [configuration] : []);

        public void Add(ReviewGateConfiguration value) => throw new NotSupportedException();
    }

    private sealed class FakeSubmissionFileRepository : ISubmissionFileRepository
    {
        private readonly List<SubmissionFile> _files = [];

        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_files.SingleOrDefault(file => file.Id == id));

        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(
            SubmissionEntityType entityType,
            Guid entityId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionFile>>(
                _files.Where(file => file.EntityType == entityType && file.EntityId == entityId).ToArray());

        public void Add(SubmissionFile submissionFile) => _files.Add(submissionFile);
    }

    private sealed class FakeSubmissionReviewRepository : ISubmissionReviewRepository
    {
        private readonly List<SubmissionReview> _reviews = [];

        public Task<IReadOnlyList<SubmissionReview>> GetBySubmissionAsync(
            ReviewGateType gateType,
            Guid submissionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionReview>>(
                _reviews.Where(review => review.GateType == gateType && review.SubmissionId == submissionId).ToArray());

        public void Add(SubmissionReview review) => _reviews.Add(review);
    }

    public sealed class FakeObjectStorage : IThesisSubmissionObjectStorage
    {
        public List<string> UploadedObjectKeys { get; } = [];

        public async Task UploadAsync(
            ThesisSubmissionObjectWriteRequest request,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            await content.CopyToAsync(Stream.Null, cancellationToken);
            UploadedObjectKeys.Add(request.ObjectKey);
        }

        public Task<Uri> CreateDownloadUriAsync(
            string objectKey,
            TimeSpan validFor,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new Uri($"https://storage.example/{objectKey}"));

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            UploadedObjectKeys.Remove(objectKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditOutbox : IThesisManagementAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];

        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IThesisUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

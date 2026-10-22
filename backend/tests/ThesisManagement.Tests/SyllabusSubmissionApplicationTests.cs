using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionHistory;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class SyllabusSubmissionApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid ReviewerId = Guid.NewGuid();

    [Fact]
    public async Task Member_can_submit_revision_after_need_revision_and_faculty_can_approve_it()
    {
        var group = CreateGroup();
        var fixtures = new WorkflowFixtures(group);

        var firstSubmission = await fixtures.CreateSubmitHandler(StudentUser())
            .HandleAsync(SubmitCommand(group.Id));
        var revisionReview = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewSyllabusCommand(group.Id, "NeedRevision", "Bổ sung phạm vi nghiên cứu."));
        var secondSubmission = await fixtures.CreateSubmitHandler(StudentUser())
            .HandleAsync(SubmitCommand(group.Id));
        var approval = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewSyllabusCommand(group.Id, "Approved", null));

        Assert.True(firstSubmission.IsSuccess);
        Assert.True(revisionReview.IsSuccess);
        Assert.True(secondSubmission.IsSuccess);
        Assert.Equal(2, secondSubmission.Value.VersionNo);
        Assert.True(approval.IsSuccess);
        Assert.Equal(StudentGroupStatus.InProgress, group.Status);
        Assert.Equal(SyllabusStatus.Approved, fixtures.Syllabus!.Status);
        Assert.Equal(2, fixtures.ObjectStorage.UploadedObjectKeys.Count);
        Assert.Collection(
            fixtures.Audit.Entries,
            entry => Assert.Equal(IntegrationAuditActions.SyllabusSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.SyllabusReviewed, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.SyllabusSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.SyllabusReviewed, entry.Action));

        var history = await fixtures.CreateHistoryHandler(StudentUser())
            .HandleAsync(new GetSyllabusSubmissionHistoryQuery(group.Id));
        Assert.True(history.IsSuccess);
        Assert.Collection(
            history.Value.Versions,
            version =>
            {
                Assert.Equal(1, version.VersionNo);
                Assert.Equal(nameof(ReviewDecision.NeedRevision), version.Review!.Decision);
            },
            version =>
            {
                Assert.Equal(2, version.VersionNo);
                Assert.Equal(nameof(ReviewDecision.Approved), version.Review!.Decision);
            });

        var download = await fixtures.CreateDownloadHandler(StudentUser())
            .HandleAsync(new GetSyllabusSubmissionFileDownloadQuery(history.Value.Versions[0].File.Id));
        Assert.True(download.IsSuccess);
        Assert.Equal(history.Value.Versions[0].File.Id, download.Value.FileId);
        Assert.StartsWith("https://storage.example/", download.Value.DownloadUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejection_stops_group_and_requires_feedback()
    {
        var group = CreateGroup();
        var fixtures = new WorkflowFixtures(group);
        await fixtures.CreateSubmitHandler(StudentUser()).HandleAsync(SubmitCommand(group.Id));

        var missingFeedback = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewSyllabusCommand(group.Id, "Rejected", "  "));
        var rejection = await fixtures.CreateReviewHandler(ReviewerUser())
            .HandleAsync(new ReviewSyllabusCommand(group.Id, "Rejected", "Chưa đạt yêu cầu học thuật."));

        Assert.True(missingFeedback.IsFailure);
        Assert.Equal(SyllabusSubmissionErrors.InvalidRequest.Code, missingFeedback.Error.Code);
        Assert.True(rejection.IsSuccess);
        Assert.Equal(StudentGroupStatus.Stopped, group.Status);
        Assert.Equal(SyllabusStatus.Rejected, fixtures.Syllabus!.Status);
    }

    [Fact]
    public async Task Only_group_members_can_submit_syllabus()
    {
        var group = CreateGroup();
        var fixtures = new WorkflowFixtures(group);
        var outsider = new FakeCurrentUser(Guid.NewGuid());

        var result = await fixtures.CreateSubmitHandler(outsider)
            .HandleAsync(SubmitCommand(group.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(SyllabusSubmissionErrors.AccessDenied.Code, result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
        Assert.Null(fixtures.Syllabus);
    }

    [Fact]
    public async Task Only_faculty_or_admin_with_review_permission_can_review_syllabus()
    {
        var group = CreateGroup();
        var fixtures = new WorkflowFixtures(group);
        await fixtures.CreateSubmitHandler(StudentUser()).HandleAsync(SubmitCommand(group.Id));

        var result = await fixtures.CreateReviewHandler(StudentUser())
            .HandleAsync(new ReviewSyllabusCommand(group.Id, "Approved", null));

        Assert.True(result.IsFailure);
        Assert.Equal(SyllabusSubmissionErrors.ReviewAccessDenied.Code, result.Error.Code);
        Assert.Equal(SyllabusStatus.Submitted, fixtures.Syllabus!.Status);
    }

    [Fact]
    public async Task Initial_submission_after_deadline_is_rejected_before_upload()
    {
        var group = CreateGroup();
        var fixtures = new WorkflowFixtures(group, now: Now.AddDays(2));

        var result = await fixtures.CreateSubmitHandler(StudentUser())
            .HandleAsync(SubmitCommand(group.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(SyllabusSubmissionErrors.InitialSubmissionDeadlinePassed.Code, result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    private static StudentGroup CreateGroup()
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1));
        group.AddMember(StudentId, BatchId, Now.AddDays(-1));
        return group;
    }

    private static SubmitSyllabusCommand SubmitCommand(Guid groupId)
        => new(groupId, "de-cuong.pdf", new MemoryStream("%PDF-1.7\ncontent"u8.ToArray()));

    private static ICurrentUser StudentUser() => new FakeCurrentUser(StudentId);

    private static ICurrentUser ReviewerUser()
        => new FakeCurrentUser(
            ReviewerId,
            permissions: [PermissionCodes.ThesisReviewGatesReview]);

    private sealed class WorkflowFixtures
    {
        private readonly FakeStudentGroupRepository _groups;
        private readonly FakeSyllabusRepository _syllabuses = new();
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
        public Syllabus? Syllabus => _syllabuses.Syllabus;

        public SubmitSyllabusHandler CreateSubmitHandler(ICurrentUser currentUser)
            => new(
                _groups,
                _syllabuses,
                _configurations,
                _files,
                new ThesisSubmissionFileValidator(),
                new ThesisSubmissionObjectKeyFactory(),
                ObjectStorage,
                Audit,
                _unitOfWork,
                currentUser,
                NowProvider);

        public ReviewSyllabusHandler CreateReviewHandler(ICurrentUser currentUser)
            => new(
                _groups,
                _syllabuses,
                _configurations,
                _reviews,
                Audit,
                _unitOfWork,
                currentUser,
                NowProvider);

        public GetSyllabusSubmissionHistoryHandler CreateHistoryHandler(ICurrentUser currentUser)
            => new(_groups, _syllabuses, _files, _reviews, currentUser);

        public GetSyllabusSubmissionFileDownloadHandler CreateDownloadHandler(ICurrentUser currentUser)
            => new(_files, _syllabuses, _groups, ObjectStorage, currentUser, NowProvider);

        private static ReviewGateConfiguration CreateConfiguration()
            => new(
                Guid.NewGuid(),
                BatchId,
                ReviewGateType.Syllabus,
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

    private sealed class FakeSyllabusRepository : ISyllabusRepository
    {
        public Syllabus? Syllabus { get; private set; }

        public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Syllabus?.Id == id ? Syllabus : null);

        public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Syllabus?.GroupId == groupId ? Syllabus : null);

        public void Add(Syllabus syllabus) => Syllabus = syllabus;
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

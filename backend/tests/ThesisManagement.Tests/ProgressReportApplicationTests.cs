using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ProgressReportApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid AdvisorId = Guid.NewGuid();

    [Fact]
    public async Task Member_can_submit_revision_and_advisor_can_accept_without_losing_history()
    {
        var fixtures = new WorkflowFixtures();
        var first = await fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project");
        var revision = await fixtures.Review(AdvisorUser(), "NeedRevision", "Bổ sung kết quả kiểm thử.");
        var second = await fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project/tree/v2");
        var accepted = await fixtures.Review(AdvisorUser(), "Accepted", null);
        var history = await fixtures.GetHistory(StudentUser());

        Assert.True(first.IsSuccess);
        Assert.False(first.Value.IsLate);
        Assert.True(revision.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value.VersionNo);
        Assert.True(accepted.IsSuccess);
        Assert.True(history.IsSuccess);
        Assert.Collection(
            history.Value.Versions,
            version =>
            {
                Assert.Equal(1, version.VersionNo);
                Assert.Equal("NeedRevision", version.Status);
                Assert.Equal("https://github.com/ugtm/team-project", version.GitUrl);
                Assert.NotNull(version.Review);
            },
            version =>
            {
                Assert.Equal(2, version.VersionNo);
                Assert.Equal("Accepted", version.Status);
                Assert.Equal("https://github.com/ugtm/team-project/tree/v2", version.GitUrl);
                Assert.NotNull(version.Review);
            });
        Assert.Equal(2, fixtures.ObjectStorage.UploadedObjectKeys.Count);
        Assert.Collection(
            fixtures.Audit.Entries,
            entry => Assert.Equal(IntegrationAuditActions.ProgressReportSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.ProgressReportReviewed, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.ProgressReportSubmitted, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.ProgressReportReviewed, entry.Action));
    }

    [Fact]
    public async Task Submission_marks_is_late_from_milestone_deadline()
    {
        var fixtures = new WorkflowFixtures(deadline: Now.AddMinutes(-1));

        var result = await fixtures.Submit(StudentUser(), "https://gitlab.com/ugtm/team-project");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsLate);
    }

    [Fact]
    public async Task Invalid_git_url_is_rejected_before_object_upload()
    {
        var fixtures = new WorkflowFixtures();

        var result = await fixtures.Submit(StudentUser(), "git@github.com:ugtm/team-project.git");

        Assert.True(result.IsFailure);
        Assert.Equal("ProgressReport.InvalidRequest", result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    [Fact]
    public async Task Other_advisor_cannot_review_progress_report()
    {
        var fixtures = new WorkflowFixtures();
        await fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project");

        var result = await fixtures.Review(
            new FakeCurrentUser(Guid.NewGuid(), roles: ["Advisor"]),
            "Accepted",
            null);

        Assert.True(result.IsFailure);
        Assert.Equal("ProgressReport.ReviewAccessDenied", result.Error.Code);
    }

    private static ICurrentUser StudentUser() => new FakeCurrentUser(StudentId, roles: ["Student"]);
    private static ICurrentUser AdvisorUser() => new FakeCurrentUser(AdvisorId, roles: ["Advisor"]);

    private sealed class WorkflowFixtures
    {
        private readonly StudentGroup _group;
        private readonly ProgressMilestone _milestone;
        private readonly FakeStudentGroupRepository _groups;
        private readonly FakeMilestoneRepository _milestones;
        private readonly FakeProgressReportRepository _reports = new();
        private readonly FakeSubmissionFileRepository _files = new();
        private readonly FakeAdvisorAssignmentContract _advisorAssignments;
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly TimeProvider _timeProvider = new FixedTimeProvider(Now);

        public WorkflowFixtures(DateTime? deadline = null)
        {
            _group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-2));
            _group.AddMember(StudentId, Guid.NewGuid(), Now.AddDays(-2));
            Assert.True(_group.TrySubmitSyllabus());
            Assert.True(_group.TryApproveSyllabus());
            _milestone = ProgressMilestone.Create(
                Guid.NewGuid(),
                _group.Id,
                "Kiểm thử hệ thống",
                null,
                deadline ?? Now.AddDays(1),
                AdvisorId,
                Now.AddDays(-1));
            _groups = new FakeStudentGroupRepository(_group);
            _milestones = new FakeMilestoneRepository(_milestone);
            _advisorAssignments = new FakeAdvisorAssignmentContract(_group.TopicId, AdvisorId);
        }

        public FakeObjectStorage ObjectStorage { get; } = new();
        public FakeAuditOutbox Audit { get; } = new();

        public Task<BuildingBlocks.Domain.Result<Application.ProgressReportSubmissions.Dtos.ProgressReportSubmissionDto>> Submit(
            ICurrentUser user,
            string gitUrl)
            => new SubmitProgressReportHandler(
                _milestones,
                _reports,
                _groups,
                _files,
                new ThesisSubmissionFileValidator(),
                new ThesisSubmissionObjectKeyFactory(),
                ObjectStorage,
                Audit,
                _unitOfWork,
                user,
                _timeProvider)
                .HandleAsync(new SubmitProgressReportCommand(
                    _milestone.Id,
                    "tien-do.pdf",
                    new MemoryStream("%PDF-1.7\nprogress"u8.ToArray()),
                    gitUrl,
                    "Ghi chú tiến độ."));

        public Task<BuildingBlocks.Domain.Result<Application.ProgressReportSubmissions.Dtos.ProgressReportReviewDto>> Review(
            ICurrentUser user,
            string decision,
            string? feedback)
            => new ReviewProgressReportHandler(
                _milestones,
                _reports,
                _groups,
                _advisorAssignments,
                Audit,
                _unitOfWork,
                user,
                _timeProvider)
                .HandleAsync(new ReviewProgressReportCommand(_milestone.Id, decision, feedback));

        public Task<BuildingBlocks.Domain.Result<Application.ProgressReportSubmissions.Dtos.ProgressReportHistoryDto>> GetHistory(ICurrentUser user)
            => new GetProgressReportHandler(
                _milestones,
                _reports,
                _groups,
                _files,
                _advisorAssignments,
                user)
                .HandleAsync(new GetProgressReportQuery(_milestone.Id));
    }

    private sealed class FakeCurrentUser(Guid userId, IEnumerable<string>? roles = null) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = (roles ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    private sealed class FakeStudentGroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StudentGroup?>(id == group.Id ? group : null);
        public void Add(StudentGroup value) => throw new NotSupportedException();
    }

    private sealed class FakeMilestoneRepository(ProgressMilestone milestone) : IProgressMilestoneRepository
    {
        public Task<ProgressMilestone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ProgressMilestone?>(id == milestone.Id ? milestone : null);
        public Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressMilestone>>(groupId == milestone.GroupId ? [milestone] : []);
        public void Add(ProgressMilestone value) => throw new NotSupportedException();
    }

    private sealed class FakeProgressReportRepository : IProgressReportRepository
    {
        private readonly List<ProgressReport> _reports = [];
        private readonly List<ProgressReportVersion> _versions = [];

        public Task<ProgressReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_reports.SingleOrDefault(report => report.Id == id));
        public Task<ProgressReport?> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default)
            => Task.FromResult(_reports.SingleOrDefault(report => report.MilestoneId == milestoneId));
        public Task<IReadOnlyList<ProgressReportVersion>> GetVersionsAsync(Guid reportId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressReportVersion>>(_versions.Where(version => version.ProgressReportId == reportId).OrderBy(version => version.VersionNo).ToArray());
        public Task<ProgressReportVersion?> GetVersionAsync(Guid reportId, int versionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.SingleOrDefault(version => version.ProgressReportId == reportId && version.VersionNo == versionNo));
        public void Add(ProgressReport report) => _reports.Add(report);
        public void AddVersion(ProgressReportVersion version) => _versions.Add(version);
    }

    private sealed class FakeSubmissionFileRepository : ISubmissionFileRepository
    {
        private readonly List<SubmissionFile> _files = [];
        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_files.SingleOrDefault(file => file.Id == id));
        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionFile>>(_files.Where(file => file.EntityType == entityType && file.EntityId == entityId).ToArray());
        public void Add(SubmissionFile file) => _files.Add(file);
    }

    private sealed class FakeAdvisorAssignmentContract(Guid topicId, Guid advisorId) : ITopicAdvisorAssignmentContract
    {
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<TopicAdvisorAssignmentContractDto?>(id == topicId ? new TopicAdvisorAssignmentContractDto(topicId, advisorId) : null);
    }

    public sealed class FakeObjectStorage : IThesisSubmissionObjectStorage
    {
        public List<string> UploadedObjectKeys { get; } = [];
        public async Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
        {
            await content.CopyToAsync(Stream.Null, cancellationToken);
            UploadedObjectKeys.Add(request.ObjectKey);
        }
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult(new Uri($"https://storage.example/{objectKey}"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            UploadedObjectKeys.Remove(objectKey);
            return Task.CompletedTask;
        }
    }

    public sealed class FakeAuditOutbox : IThesisManagementAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IThesisUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

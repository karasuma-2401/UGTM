using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
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

    [Fact]
    public async Task Pending_member_cannot_submit_or_read_progress_report()
    {
        var pendingId = Guid.NewGuid();
        var fixtures = new WorkflowFixtures(pendingId: pendingId);
        var pending = new FakeCurrentUser(pendingId, roles: ["Student"]);

        var denied = await fixtures.Submit(pending, "https://github.com/ugtm/team-project");
        var submitted = await fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project");
        var history = await fixtures.GetHistory(pending);

        Assert.Equal("ProgressReport.AccessDenied", denied.Error.Code);
        Assert.True(submitted.IsSuccess);
        Assert.True(history.IsFailure);
    }

    [Fact]
    public async Task Stopped_group_cannot_submit_even_with_approved_syllabus()
    {
        var fixtures = new WorkflowFixtures();
        Assert.True(fixtures.Group.TryStop());

        var result = await fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project");

        Assert.Equal("ProgressReport.InvalidSubmissionState", result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    [Fact]
    public async Task Ambiguous_save_failure_keeps_uploaded_progress_object()
    {
        var fixtures = new WorkflowFixtures { FailSave = true };

        await Assert.ThrowsAsync<IOException>(() => fixtures.Submit(StudentUser(), "https://github.com/ugtm/team-project"));

        Assert.Single(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    [Fact]
    public async Task Member_can_submit_up_to_five_categorized_links_and_evidence_images()
    {
        var fixtures = new WorkflowFixtures();
        var links = new SubmissionProductLinkInput[]
        {
            new("SourceCode", "Backend API", "https://github.com/ugtm/backend"),
            new("Demo", "Video Demo", "https://youtu.be/demo123"),
            new("Documentation", "Figma UI", "https://figma.com/file/xyz"),
            new("Other", "Slide", "https://docs.google.com/presentation/1"),
            new("SourceCode", "Mobile App", "https://github.com/ugtm/mobile"),
        };
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var images = new SubmissionAttachmentUpload[]
        {
            new("screen-1.png", new MemoryStream(pngHeader)),
            new("screen-2.png", new MemoryStream(pngHeader)),
        };

        var submitResult = await fixtures.Submit(StudentUser(), null, links, images);
        var history = await fixtures.GetHistory(StudentUser());

        Assert.True(submitResult.IsSuccess);
        Assert.Equal("https://github.com/ugtm/backend", submitResult.Value.GitUrl);
        Assert.Equal(5, submitResult.Value.ProductLinks?.Count);
        Assert.Equal(2, submitResult.Value.Attachments?.Count);
        Assert.Equal(3, fixtures.ObjectStorage.UploadedObjectKeys.Count);
        Assert.True(history.IsSuccess);
        var version = Assert.Single(history.Value.Versions);
        Assert.Equal(5, version.ProductLinks?.Count);
        Assert.Equal(2, version.Attachments?.Count);
    }

    [Fact]
    public async Task Fake_png_evidence_image_is_rejected_before_object_upload()
    {
        var fixtures = new WorkflowFixtures();
        var links = new SubmissionProductLinkInput[]
        {
            new("SourceCode", "Repo", "https://github.com/ugtm/backend"),
        };
        var fakeImages = new SubmissionAttachmentUpload[]
        {
            new("not-an-image.png", new MemoryStream("not really a png"u8.ToArray())),
        };

        var result = await fixtures.Submit(StudentUser(), null, links, fakeImages);

        Assert.True(result.IsFailure);
        Assert.Equal("ProgressReport.InvalidAttachment", result.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
    }

    [Fact]
    public async Task More_than_five_links_or_five_images_are_rejected_before_object_upload()
    {
        var fixtures = new WorkflowFixtures();
        var sixLinks = Enumerable.Range(1, 6)
            .Select(i => new SubmissionProductLinkInput("SourceCode", $"Link {i}", $"https://github.com/ugtm/repo-{i}"))
            .ToArray();

        var tooManyLinksResult = await fixtures.Submit(StudentUser(), null, sixLinks);
        Assert.True(tooManyLinksResult.IsFailure);
        Assert.Equal("ProgressReport.InvalidRequest", tooManyLinksResult.Error.Code);

        var validLink = new SubmissionProductLinkInput[] { new("SourceCode", "Repo", "https://github.com/ugtm/repo") };
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var sixImages = Enumerable.Range(1, 6)
            .Select(i => new SubmissionAttachmentUpload($"img-{i}.png", new MemoryStream(pngHeader)))
            .ToArray();

        var tooManyImagesResult = await fixtures.Submit(StudentUser(), null, validLink, sixImages);
        Assert.True(tooManyImagesResult.IsFailure);
        Assert.Equal("ProgressReport.InvalidAttachment", tooManyImagesResult.Error.Code);
        Assert.Empty(fixtures.ObjectStorage.UploadedObjectKeys);
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
        private readonly FakeSubmissionProductLinkRepository _productLinks = new();
        private readonly FakeSubmissionAttachmentRepository _attachments = new();
        private readonly FakeAdvisorAssignmentContract _advisorAssignments;
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly TimeProvider _timeProvider = new FixedTimeProvider(Now);

        public WorkflowFixtures(DateTime? deadline = null, Guid? pendingId = null)
        {
            _group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-2));
            var batchId = Guid.NewGuid();
            _group.AddMember(StudentId, batchId, Now.AddDays(-2));
            if (pendingId.HasValue)
                _group.AddPendingMember(pendingId.Value, batchId, StudentId, "token", Now.AddDays(-1), Now.AddDays(1));
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
        public StudentGroup Group => _group;
        public bool FailSave { set => _unitOfWork.FailSave = value; }

        public Task<BuildingBlocks.Domain.Result<Application.ProgressReportSubmissions.Dtos.ProgressReportSubmissionDto>> Submit(
            ICurrentUser user,
            string? gitUrl,
            IReadOnlyList<SubmissionProductLinkInput>? productLinks = null,
            IReadOnlyList<SubmissionAttachmentUpload>? evidenceImages = null)
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
                _timeProvider,
                new FakeThesisUploadLifecycle(),
                new ApprovedSyllabusRepository(_group.Id),
                _productLinks,
                _attachments,
                new ThesisSubmissionImageValidator())
                .HandleAsync(new SubmitProgressReportCommand(
                    _milestone.Id,
                    "tien-do.pdf",
                    new MemoryStream("%PDF-1.7\nprogress"u8.ToArray()),
                    gitUrl,
                    "Ghi chú tiến độ.",
                    productLinks,
                    evidenceImages));

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
                user,
                _productLinks,
                _attachments)
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

    private sealed class FakeSubmissionProductLinkRepository : ISubmissionProductLinkRepository
    {
        private readonly List<SubmissionProductLink> _links = [];
        public Task<IReadOnlyList<SubmissionProductLink>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionProductLink>>(
                _links.Where(link => link.EntityType == entityType && link.EntityId == entityId)
                    .OrderBy(link => link.DisplayOrder)
                    .ToArray());
        public void AddRange(IEnumerable<SubmissionProductLink> links) => _links.AddRange(links);
    }

    private sealed class FakeSubmissionAttachmentRepository : ISubmissionAttachmentRepository
    {
        private readonly List<SubmissionAttachment> _attachments = [];
        public Task<SubmissionAttachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_attachments.SingleOrDefault(attachment => attachment.Id == id));
        public Task<IReadOnlyList<SubmissionAttachment>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionAttachment>>(
                _attachments.Where(attachment => attachment.EntityType == entityType && attachment.EntityId == entityId)
                    .OrderBy(attachment => attachment.DisplayOrder)
                    .ToArray());
        public void AddRange(IEnumerable<SubmissionAttachment> attachments) => _attachments.AddRange(attachments);
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
        public bool FailSave { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => FailSave ? Task.FromException<int>(new IOException("Save outcome unknown")) : Task.FromResult(1);
    }

    private sealed class ApprovedSyllabusRepository : ISyllabusRepository
    {
        private readonly Syllabus _syllabus;

        public ApprovedSyllabusRepository(Guid groupId)
        {
            _syllabus = Syllabus.CreateSubmitted(Guid.NewGuid(), groupId);
            _syllabus.RecordReview(ReviewDecision.Approved, null, Guid.NewGuid(), Now);
        }

        public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Syllabus?>(_syllabus.Id == id ? _syllabus : null);
        public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<Syllabus?>(_syllabus.GroupId == groupId ? _syllabus : null);
        public void Add(Syllabus syllabus) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase4Group4Tests
{
    private static readonly Guid BatchId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TopicId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid AdvisorId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid StudentAId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static readonly Guid StudentBId = Guid.Parse("50000000-0000-0000-0000-000000000005");
    private static readonly Guid FacultyId = Guid.Parse("60000000-0000-0000-0000-000000000006");
    private static readonly DateTime NowUtc = new(2026, 11, 26, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Milestone_create_submit_and_review_enqueue_expected_SMTP_and_in_app_notifications()
    {
        var group = CreateInProgressGroup();
        var milestoneRepo = new FakeProgressMilestoneRepository();
        var reportRepo = new FakeProgressReportRepository();
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var outbox = new CapturingThesisNotificationOutbox();

        var createHandler = new CreateProgressMilestoneHandler(
            new FakeGroupRepository(group),
            milestoneRepo,
            advisorContract,
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(AdvisorId, ["Lecturer"], []),
            new FixedTimeProvider(NowUtc),
            notificationOutbox: outbox);

        var created = await createHandler.HandleAsync(new CreateProgressMilestoneCommand(
            group.Id,
            "Hoàn thiện kiến trúc dữ liệu",
            "Nộp thiết kế ERD và API",
            NowUtc.AddDays(7)));

        Assert.True(created.IsSuccess);
        var createdEntry = Assert.Single(outbox.Entries);
        Assert.Equal("MILESTONE_CREATED", createdEntry.NotificationType);
        Assert.False(createdEntry.EnqueueEmail);
        Assert.Equal([StudentAId, StudentBId], createdEntry.RecipientUserIds);
        Assert.Equal("/thesis/milestones", createdEntry.ActionPath);

        outbox.Entries.Clear();
        var submitHandler = new SubmitProgressReportHandler(
            milestoneRepo,
            reportRepo,
            new FakeGroupRepository(group),
            new FakeSubmissionFileRepository(),
            new FakeFileValidator(".pdf"),
            new FakeObjectKeyFactory(),
            new FakeObjectStorage(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StudentAId, ["Student"], []),
            new FixedTimeProvider(NowUtc.AddMinutes(10)),
            new FakeUploadLifecycle(),
            notificationOutbox: outbox,
            topicSummaryReader: new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Hệ thống thông báo KLTN"),
            studentInfoReader: new FakeStudentInfoReader(StudentAId, "Nguyễn Văn A"));

        await using var reportStream = new MemoryStream([1, 2, 3]);
        var submitted = await submitHandler.HandleAsync(new SubmitProgressReportCommand(
            created.Value.Id,
            "moc-1.pdf",
            reportStream,
            "https://github.com/uit/ugtm",
            "Đã hoàn thành mô-đun cơ sở"));

        Assert.True(submitted.IsSuccess);
        Assert.Equal(2, outbox.Entries.Count);

        var confirmed = Assert.Single(outbox.Entries, e => e.NotificationType == "SUBMISSION_CONFIRMED");
        Assert.Equal("Critical", confirmed.Priority);
        Assert.True(confirmed.EnqueueEmail);
        Assert.Equal("/thesis/milestones", confirmed.ActionPath);
        Assert.Equal([StudentAId, StudentBId], confirmed.RecipientUserIds);

        var advisorAlert = Assert.Single(outbox.Entries, e => e.NotificationType == "PROGRESS_REPORT_SUBMITTED");
        Assert.False(advisorAlert.EnqueueEmail);
        Assert.Equal([AdvisorId], advisorAlert.RecipientUserIds);
        Assert.Equal($"/advisor/milestones/{created.Value.Id}/review", advisorAlert.ActionPath);

        outbox.Entries.Clear();
        var reviewHandler = new ReviewProgressReportHandler(
            milestoneRepo,
            reportRepo,
            new FakeGroupRepository(group),
            advisorContract,
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(AdvisorId, ["Lecturer"], []),
            new FixedTimeProvider(NowUtc.AddMinutes(20)),
            outbox);

        var reviewed = await reviewHandler.HandleAsync(new ReviewProgressReportCommand(
            created.Value.Id,
            "NeedRevision",
            "Bổ sung chỉ mục chống trùng thông báo."));

        Assert.True(reviewed.IsSuccess);
        var reviewEntry = Assert.Single(outbox.Entries);
        Assert.Equal("PROGRESS_REPORT_REVISION_REQUIRED", reviewEntry.NotificationType);
        Assert.False(reviewEntry.EnqueueEmail);
        Assert.Equal("/thesis/milestones", reviewEntry.ActionPath);
    }

    [Fact]
    public async Task SubmitFinalReportHandler_enqueues_SUBMISSION_CONFIRMED_email_and_advisor_grading_alert()
    {
        var group = CreateInProgressGroup();
        var midterm = MidtermReport.CreateSubmitted(Guid.NewGuid(), group.Id);
        midterm.RecordReview(ReviewDecision.Approved, "Đạt giữa kỳ", FacultyId, NowUtc.AddDays(-2));

        var eligibilityChecker = new FinalReportSubmissionEligibilityChecker(
            new FakeGroupRepository(group),
            new FakeMidtermRepository(midterm),
            new FakeBatchReader(),
            new FakeUser(StudentAId, ["Student"], []),
            new FixedTimeProvider(NowUtc));

        var outbox = new CapturingThesisNotificationOutbox();
        var handler = new SubmitFinalReportHandler(
            eligibilityChecker,
            new FakeFinalReportRepository(),
            new FakeSubmissionFileRepository(),
            new FakePlagiarismScanRepository(),
            new FakeFileValidator(".pdf"),
            new FakeObjectKeyFactory(),
            new FakeObjectStorage(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            NullLogger<SubmitFinalReportHandler>.Instance,
            notificationOutbox: outbox,
            topicSummaryReader: new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Hệ thống thông báo KLTN"),
            studentInfoReader: new FakeStudentInfoReader(StudentAId, "Nguyễn Văn A"));

        await using var stream = new MemoryStream([4, 5, 6]);
        var result = await handler.HandleAsync(new SubmitFinalReportCommand(group.Id, "bao-cao-cuoi-ky.pdf", stream));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, outbox.Entries.Count);

        var confirmed = Assert.Single(outbox.Entries, e => e.NotificationType == "SUBMISSION_CONFIRMED");
        Assert.Equal("Critical", confirmed.Priority);
        Assert.True(confirmed.EnqueueEmail);
        Assert.Equal("/thesis/final-report", confirmed.ActionPath);
        Assert.Equal([StudentAId, StudentBId], confirmed.RecipientUserIds);

        var advisorAlert = Assert.Single(outbox.Entries, e => e.NotificationType == "FINAL_REPORT_SUBMITTED");
        Assert.False(advisorAlert.EnqueueEmail);
        Assert.Equal([AdvisorId], advisorAlert.RecipientUserIds);
        Assert.Equal("/thesis/lecturer-grading", advisorAlert.ActionPath);
    }

    [Fact]
    public async Task Archival_Submit_AdvisorConfirm_and_FacultyConfirm_enqueue_expected_notifications()
    {
        var group = CreateInProgressGroup();
        var store = new FakeFinalSubmissionStore(group);
        var topicReader = new FakeArchivalTopicReader(TopicId, BatchId, AdvisorId, "Hệ thống lưu chiểu số KLTN");
        var batchReader = new FakeArchivalBatchReader(BatchId, NowUtc.AddDays(-1), NowUtc.AddDays(7));
        var defenseProof = new FakeLockedDefenseProofReader(group.Id, BatchId, NowUtc.AddDays(-2));
        var coordinator = new FakeFinalSubmissionCoordinator(store);
        var outbox = new CapturingThesisNotificationOutbox();

        var studentAccess = new FinalSubmissionAccess(
            store,
            new FakeUser(StudentAId, ["Student"], []),
            new FakeAuthorityReader(StudentAId, ["Student"], []),
            topicReader,
            batchReader);

        var submitHandler = new SubmitFinalSubmissionHandler(
            store,
            studentAccess,
            defenseProof,
            coordinator,
            new DualExtFileValidator(),
            new FakeObjectKeyFactory(),
            new FakeObjectStorage(),
            new NoOpArchivalCleanup(),
            new FakeThesisAuditOutbox(),
            new FixedTimeProvider(NowUtc),
            NullLogger<SubmitFinalSubmissionHandler>.Instance,
            outbox,
            new FakeStudentInfoReader(StudentAId, "Nguyễn Văn A"));

        await using var pdfStream = new MemoryStream([1, 2, 3]);
        await using var docxStream = new MemoryStream([4, 5, 6]);
        var submitResult = await submitHandler.HandleAsync(new SubmitFinalSubmissionCommand(
            group.Id,
            ExpectedSubmissionVersion: null,
            Key: Guid.NewGuid(),
            AbstractVi: "Tóm tắt tiếng Việt cho khóa luận tốt nghiệp.",
            AbstractEn: "English abstract for graduation thesis.",
            KeywordsVi: "KLTN, lưu chiểu",
            KeywordsEn: "thesis, archival",
            PdfName: "kltn-final.pdf",
            Pdf: pdfStream,
            DocxName: "kltn-final.docx",
            Docx: docxStream));

        Assert.True(submitResult.IsSuccess);
        var confirmed = Assert.Single(outbox.Entries, e => e.NotificationType == "SUBMISSION_CONFIRMED");
        Assert.True(confirmed.EnqueueEmail);
        Assert.Equal("/thesis/archival", confirmed.ActionPath);
        Assert.Contains(confirmed.EmailMetadata!, m => m.Label == "Tệp PDF" && m.Value.Contains("kltn-final.pdf"));
        Assert.Contains(confirmed.EmailMetadata!, m => m.Label == "Tệp DOCX" && m.Value.Contains("kltn-final.docx"));

        var advisorSubmitted = Assert.Single(outbox.Entries, e => e.NotificationType == "FINAL_SUBMISSION_SUBMITTED");
        Assert.False(advisorSubmitted.EnqueueEmail);
        Assert.Equal("/advisor/archival", advisorSubmitted.ActionPath);

        outbox.Entries.Clear();
        var advisorAccess = new FinalSubmissionAccess(
            store,
            new FakeUser(AdvisorId, ["Lecturer"], []),
            new FakeAuthorityReader(AdvisorId, ["Lecturer"], []),
            topicReader,
            batchReader);
        var advisorConfirmHandler = new ConfirmFinalSubmissionHandler(
            store,
            advisorAccess,
            defenseProof,
            coordinator,
            new FakeThesisAuditOutbox(),
            new FixedTimeProvider(NowUtc.AddMinutes(5)),
            outbox);

        var advisorConfirm = await advisorConfirmHandler.HandleAsync(new ConfirmFinalSubmissionCommand(
            store.Submission!.Id,
            FinalSubmissionReviewAction.AdvisorConfirm,
            ExpectedSubmissionVersion: store.Submission!.Version,
            ExpectedPackageVersion: 1,
            Key: Guid.NewGuid()));

        Assert.True(advisorConfirm.IsSuccess);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "FINAL_SUBMISSION_ADVISOR_CONFIRMED" && e.ActionPath == "/thesis/review/archival");
        Assert.Contains(outbox.Entries, e => e.NotificationType == "FINAL_SUBMISSION_ADVISOR_CONFIRMED" && e.ActionPath == "/thesis/archival");

        outbox.Entries.Clear();
        var facultyAccess = new FinalSubmissionAccess(
            store,
            new FakeUser(FacultyId, ["Faculty"], [PermissionCodes.ThesisFinalSubmissionsManage, PermissionCodes.ThesisFinalSubmissionsRead]),
            new FakeAuthorityReader(FacultyId, ["Faculty"], [PermissionCodes.ThesisFinalSubmissionsManage, PermissionCodes.ThesisFinalSubmissionsRead]),
            topicReader,
            batchReader);
        var facultyConfirmHandler = new ConfirmFinalSubmissionHandler(
            store,
            facultyAccess,
            defenseProof,
            coordinator,
            new FakeThesisAuditOutbox(),
            new FixedTimeProvider(NowUtc.AddMinutes(10)),
            outbox);

        var facultyConfirm = await facultyConfirmHandler.HandleAsync(new ConfirmFinalSubmissionCommand(
            store.Submission!.Id,
            FinalSubmissionReviewAction.FacultyConfirm,
            ExpectedSubmissionVersion: store.Submission!.Version,
            ExpectedPackageVersion: 1,
            Key: Guid.NewGuid()));

        Assert.True(facultyConfirm.IsSuccess);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "FINAL_SUBMISSION_ARCHIVED" && e.ActionPath == "/thesis/archival");
        Assert.Contains(outbox.Entries, e => e.NotificationType == "FINAL_SUBMISSION_ARCHIVED" && e.ActionPath == "/advisor/archival");
    }

    private static StudentGroup CreateInProgressGroup()
    {
        var group = new StudentGroup(Guid.NewGuid(), TopicId, NowUtc.AddDays(-20));
        group.AddConfirmedMember(StudentAId, BatchId, NowUtc.AddDays(-20));
        group.AddConfirmedMember(StudentBId, BatchId, NowUtc.AddDays(-19));
        group.TrySubmitSyllabus();
        group.TryApproveSyllabus();
        return group;
    }

    private sealed class CapturingThesisNotificationOutbox : IThesisNotificationOutbox
    {
        public List<NotificationOutboxEntry> Entries { get; } = [];
        public void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeGroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(group.Id == id ? group : null);
        public void Add(StudentGroup value) { }
    }

    private sealed class FakeProgressMilestoneRepository : IProgressMilestoneRepository
    {
        private ProgressMilestone? _stored;
        public Task<ProgressMilestone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressMilestone>>(_stored is null ? [] : [_stored]);
        public void Add(ProgressMilestone milestone) => _stored = milestone;
    }

    private sealed class FakeProgressReportRepository : IProgressReportRepository
    {
        private ProgressReport? _report;
        private readonly List<ProgressReportVersion> _versions = [];
        public Task<ProgressReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_report?.Id == id ? _report : null);
        public Task<ProgressReport?> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default)
            => Task.FromResult(_report?.MilestoneId == milestoneId ? _report : null);
        public Task<IReadOnlyList<ProgressReportVersion>> GetVersionsAsync(Guid progressReportId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressReportVersion>>(_versions);
        public Task<ProgressReportVersion?> GetVersionAsync(Guid progressReportId, int versionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(_versions.SingleOrDefault(v => v.ProgressReportId == progressReportId && v.VersionNo == versionNo));
        public void Add(ProgressReport report) => _report = report;
        public void AddVersion(ProgressReportVersion version) => _versions.Add(version);
    }

    private sealed class FakeMidtermRepository(MidtermReport report) : IMidtermReportRepository
    {
        public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(report.Id == id ? report : null);
        public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(report.GroupId == groupId ? report : null);
        public void Add(MidtermReport midtermReport) { }
    }

    private sealed class FakeFinalReportRepository : IFinalReportRepository
    {
        private FinalReport? _stored;
        public Task<FinalReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<FinalReport?> GetByGroupAndVersionTypeAsync(Guid groupId, FinalReportVersionType versionType, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.GroupId == groupId && _stored.VersionType == versionType ? _stored : null);
        public void Add(FinalReport finalReport) => _stored = finalReport;
        public void TrackGroupConcurrency(FinalReport finalReport, StudentGroup group) { }
    }

    private sealed class FakePlagiarismScanRepository : IFinalReportPlagiarismScanRepository
    {
        public Task<FinalReportPlagiarismScan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<FinalReportPlagiarismScan?>(null);
        public Task<FinalReportPlagiarismScan?> GetByReportAndVersionAsync(Guid finalReportId, int versionNo, CancellationToken cancellationToken = default) => Task.FromResult<FinalReportPlagiarismScan?>(null);
        public Task<FinalReportPlagiarismScan?> GetLatestByReportIdAsync(Guid finalReportId, CancellationToken cancellationToken = default) => Task.FromResult<FinalReportPlagiarismScan?>(null);
        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetByReportIdAsync(Guid finalReportId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>([]);
        public Task<FinalReportPlagiarismScan?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default) => Task.FromResult<FinalReportPlagiarismScan?>(null);
        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetActiveOrPendingScansAsync(int limit = 50, string? workerPartition = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>([]);
        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetResumableScansAsync(int limit = 50, string? workerPartition = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>([]);
        public void Add(FinalReportPlagiarismScan scan) { }
        public void Update(FinalReportPlagiarismScan scan) { }
    }

    private sealed class FakeFinalSubmissionStore(StudentGroup group) : IFinalSubmissionStore
    {
        public FinalSubmission? Submission { get; private set; }
        private readonly List<FinalSubmissionPackage> _packages = [];
        private readonly List<SubmissionFile> _files = [];
        private readonly List<FinalSubmissionPackageFile> _links = [];
        private readonly List<FinalSubmissionReview> _reviews = [];
        private readonly List<FinalSubmissionIdempotency> _receipts = [];
        private FinalSubmissionUploadAttempt? _attempt;

        public void BumpVersion()
        {
            if (Submission is not null)
            {
                Submission.Version++;
            }
        }

        public Task<StudentGroup?> GetGroupAsync(Guid groupId, bool locked, CancellationToken token)
            => Task.FromResult(group.Id == groupId ? group : null);
        public Task<FinalSubmission?> GetByGroupAsync(Guid groupId, bool locked, CancellationToken token)
            => Task.FromResult(Submission?.GroupId == groupId ? Submission : null);
        public Task<FinalSubmission?> GetByIdAsync(Guid id, CancellationToken token)
            => Task.FromResult(Submission?.Id == id ? Submission : null);
        public Task<FinalSubmissionPackageGraph> GetPackagesAsync(Guid submissionId, CancellationToken token)
            => Task.FromResult(new FinalSubmissionPackageGraph(_packages, _links, _files, _reviews));
        public Task<FinalSubmissionPackageGraph> LockCurrentPackageAsync(Guid submissionId, Guid packageId, CancellationToken token)
            => Task.FromResult(new FinalSubmissionPackageGraph(_packages, _links, _files, _reviews));
        public Task<IReadOnlyDictionary<Guid, Guid>> GetGroupTopicsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken token)
            => Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid> { [group.Id] = group.TopicId });
        public Task<(IReadOnlyList<FinalSubmission> Rows, int Count)> ListAsync(IReadOnlyCollection<Guid> topicIds, Guid? groupId, string? status, int page, int size, CancellationToken token)
            => Task.FromResult(((IReadOnlyList<FinalSubmission>)(Submission is null ? [] : [Submission]), Submission is null ? 0 : 1));
        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => Task.CompletedTask;
        public Task<FinalSubmissionIdempotency?> GetReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token)
            => Task.FromResult(_receipts.SingleOrDefault(r => r.ActorId == actor && r.Operation == operation && r.IdempotencyKey == key));
        public void Add(FinalSubmission submission) => Submission = submission;
        public void Add(FinalSubmissionPackage package, IReadOnlyList<SubmissionFile> files, IReadOnlyList<FinalSubmissionPackageFile> links)
        {
            _packages.Add(package);
            _files.AddRange(files);
            _links.AddRange(links);
        }
        public void Add(FinalSubmissionIdempotency receipt) => _receipts.Add(receipt);
        public void Add(FinalSubmissionReview review) => _reviews.Add(review);
        public Task CreateAttemptAsync(FinalSubmissionUploadAttempt attempt, CancellationToken token)
        {
            _attempt = attempt;
            return Task.CompletedTask;
        }
        public Task<FinalSubmissionUploadAttempt?> LockAttemptAsync(Guid attemptId, CancellationToken token)
            => Task.FromResult(_attempt?.Id == attemptId ? _attempt : null);
    }

    private sealed class FakeFinalSubmissionCoordinator(FakeFinalSubmissionStore store) : IFinalSubmissionWorkflowTransactionCoordinator
    {
        public async Task<T> ExecuteWithPostSaveAsync<T>(
            Func<CancellationToken, Task<FinalSubmissionTransactionResult<T>>> workflow,
            Func<T, CancellationToken, Task<T>> postSave,
            CancellationToken token = default)
        {
            var outcome = await workflow(token);
            if (!outcome.ShouldCommit)
            {
                return outcome.Value;
            }

            store.BumpVersion();
            return await postSave(outcome.Value, token);
        }
    }

    private sealed class FakeArchivalTopicReader(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : IReviewerAssignmentTopicReader
    {
        private readonly ReviewerAssignmentTopicContractDto _dto = new(topicId, batchId, advisorId, titleVi, null);
        public Task<IReadOnlyList<ReviewerAssignmentTopicContractDto>> GetManyAsync(IReadOnlyCollection<Guid> topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentTopicContractDto>>([_dto]);
        public Task<IReadOnlyList<Guid>> FindIdsAsync(Guid? batchId, string? keyword, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([topicId]);
        public Task<ReviewerAssignmentTopicContractDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == topicId ? _dto : null);
        public Task<ReviewerAssignmentTopicContractDto?> LockAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == topicId ? _dto : null);
    }

    private sealed class FakeArchivalBatchReader(Guid batchId, DateTime opensAtUtc, DateTime deadlineUtc) : IFinalSubmissionBatchReader
    {
        private readonly FinalSubmissionBatchContractDto _dto = new(batchId, "Open", opensAtUtc, deadlineUtc, "fp-1");
        public Task<FinalSubmissionBatchContractDto?> GetAsync(Guid id, CancellationToken token = default)
            => Task.FromResult(id == batchId ? _dto : null);
        public Task<FinalSubmissionBatchContractDto?> LockAsync(Guid id, CancellationToken token = default)
            => Task.FromResult(id == batchId ? _dto : null);
    }

    private sealed class FakeAuthorityReader(Guid userId, IReadOnlyList<string> roles, IReadOnlyList<string> permissions) : IFinalSubmissionAuthorityReader
    {
        private readonly FinalSubmissionAuthorityDto _dto = new(userId, true, roles, permissions);
        public Task<FinalSubmissionAuthorityDto?> GetAsync(Guid id, CancellationToken token = default)
            => Task.FromResult(id == userId ? _dto : null);
        public Task<FinalSubmissionAuthorityDto?> LockAsync(Guid id, CancellationToken token = default)
            => Task.FromResult(id == userId ? _dto : null);
    }

    private sealed class FakeLockedDefenseProofReader(Guid groupId, Guid batchId, DateTime lockedAtUtc) : ILockedDefenseProofReader
    {
        private readonly LockedDefenseProofContractDto _dto = new(
            LockedDefenseOutcome.Succeeded,
            groupId,
            batchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            lockedAtUtc,
            new string('b', 64));

        public Task<LockedDefenseProofContractDto> GetAsync(Guid id, CancellationToken token = default) => Task.FromResult(_dto);
        public Task<LockedDefenseProofContractDto> LockAsync(Guid id, CancellationToken token = default) => Task.FromResult(_dto);
    }

    private sealed class NoOpArchivalCleanup : IFinalSubmissionUploadCleanup
    {
        public Task CleanAsync(Guid? attemptId, CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class DualExtFileValidator : IThesisSubmissionFileValidator
    {
        public Task<ValidatedThesisSubmissionFile> ValidateAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = ext == ".docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/pdf";
            var sha = ext == ".docx" ? new string('d', 64) : new string('c', 64);
            return Task.FromResult(new ValidatedThesisSubmissionFile(
                fileName,
                ext,
                contentType,
                512_000,
                sha,
                new byte[] { 1, 2, 3, 4 }));
        }
    }

    private sealed class FakeAdvisorAssignmentContract(Guid topicId, Guid advisorId) : ITopicAdvisorAssignmentContract
    {
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == topicId ? new TopicAdvisorAssignmentContractDto(topicId, advisorId, BatchId) : null);
    }

    private sealed class FakeSubmissionFileRepository : ISubmissionFileRepository
    {
        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SubmissionFile?>(null);
        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionFile>>([]);
        public void Add(SubmissionFile submissionFile) { }
    }

    private sealed class FakeFileValidator(string ext) : IThesisSubmissionFileValidator
    {
        public Task<ValidatedThesisSubmissionFile> ValidateAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidatedThesisSubmissionFile(fileName, ext, "application/pdf", 320_000, new string('a', 64), new byte[] { 1, 2, 3 }));
    }

    private sealed class FakeObjectKeyFactory : IThesisSubmissionObjectKeyFactory
    {
        public string Create(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension) => $"{entityType}/{entityId}/v{versionNo}{fileExtension}";
        public string CreateAttachment(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension) => $"{entityType}/{entityId}/att-v{versionNo}{fileExtension}";
    }

    private sealed class FakeObjectStorage : IThesisSubmissionObjectStorage
    {
        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default) => Task.FromResult(new Uri("https://example.test/dl"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUploadLifecycle : IThesisUploadLifecycle
    {
        public Task<Guid> BeginAsync(string objectKey, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Error?> SaveAsync(Guid attemptId, Func<CancellationToken, Task<Error?>> save, Error expiredError, CancellationToken cancellationToken = default) => save(cancellationToken);
        public Task FinishAsync(Guid attemptId) => Task.CompletedTask;
    }

    private sealed class FakeBatchReader : IKltnBatchReader
    {
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<BatchRegistrationWindowContractDto?>(null);
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<BatchTopicPreparationWindowContractDto?>(null);
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult<BatchRenameRequestWindowContractDto?>(null);
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchFinalReportSubmissionWindowContractDto?>(new(batchId, "Open", true, nowUtc.AddDays(-3), nowUtc.AddDays(10)));
    }

    private sealed class FakeTopicSummaryReader(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : ITopicSummaryReader
    {
        public Task<IReadOnlyDictionary<Guid, TopicSummaryContractDto>> GetByIdsAsync(IEnumerable<Guid> topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TopicSummaryContractDto>>(new Dictionary<Guid, TopicSummaryContractDto>
            {
                [topicId] = new(topicId, batchId, advisorId, titleVi, null)
            });
    }

    private sealed class FakeStudentInfoReader(Guid studentId, string fullName) : IStudentInfoReader
    {
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(new Dictionary<Guid, StudentInfoContractDto>());
        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? id, string? userCode, CancellationToken cancellationToken = default)
            => Task.FromResult(id == studentId ? new StudentInfoContractDto(studentId, "sv@gm.uit.edu.vn", fullName, "22520001", null) : null);
    }

    private sealed class FakeThesisAuditOutbox : IThesisManagementAuditOutbox
    {
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) { }
    }

    private sealed class FakeThesisUnitOfWork : IThesisUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeUser(Guid userId, IEnumerable<string> roles, IEnumerable<string> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = permissions.ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase4Group3Tests
{
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TopicId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdvisorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StudentAId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid StudentBId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid StaffActorId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTime NowUtc = new(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SubmitSyllabusHandler_enqueues_critical_SUBMISSION_CONFIRMED_email_and_in_app_alerts()
    {
        var group = CreateTwoStudentGroup();
        var gateConfig = CreateGateConfiguration(ReviewGateType.Syllabus);
        var outbox = new CapturingThesisNotificationOutbox();

        var handler = new SubmitSyllabusHandler(
            new FakeGroupRepository(group),
            new FakeSyllabusRepository(),
            new FakeGateConfigRepository(gateConfig),
            new FakeSubmissionFileRepository(),
            new FakeFileValidator(),
            new FakeObjectKeyFactory(),
            new FakeObjectStorage(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StudentAId, ["Student"], []),
            new FixedTimeProvider(NowUtc),
            new FakeUploadLifecycle(),
            new FakeBatchReader(),
            outbox,
            new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Hệ thống trợ lý học vụ đa tác tử"),
            new FakeStudentInfoReader(StudentAId, "Nguyễn Văn A"));

        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var result = await handler.HandleAsync(new SubmitSyllabusCommand(group.Id, "de-cuong-v1.pdf", stream));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, outbox.Entries.Count);

        var confirmed = Assert.Single(outbox.Entries, e => e.NotificationType == "SUBMISSION_CONFIRMED");
        Assert.Equal("Critical", confirmed.Priority);
        Assert.True(confirmed.EnqueueEmail);
        Assert.Equal([StudentAId, StudentBId], confirmed.RecipientUserIds);
        Assert.Equal("/thesis/proposal", confirmed.ActionPath);
        Assert.NotNull(confirmed.EmailMetadata);
        Assert.Contains(confirmed.EmailMetadata!, m => m.Label == "Người nộp" && m.Value == "Nguyễn Văn A");
        Assert.Contains(confirmed.EmailMetadata!, m => m.Label == "Tệp ghi nhận" && m.Value.Contains("de-cuong-v1.pdf"));

        var facultyAlert = Assert.Single(outbox.Entries, e => e.NotificationType == "SYLLABUS_SUBMITTED" && e.ActionPath == "/thesis/review/syllabus");
        Assert.False(facultyAlert.EnqueueEmail);
        Assert.Contains(PermissionCodes.ThesisReviewGatesReview, facultyAlert.RecipientPermissions!);

        var advisorAlert = Assert.Single(outbox.Entries, e => e.NotificationType == "SYLLABUS_SUBMITTED" && e.ActionPath == "/topics/advising");
        Assert.False(advisorAlert.EnqueueEmail);
        Assert.Equal([AdvisorId], advisorAlert.RecipientUserIds);
    }

    [Fact]
    public async Task ReviewSyllabusHandler_Rejected_enqueues_critical_GROUP_STOPPED_for_students_and_advisor()
    {
        var group = CreateTwoStudentGroup();
        Assert.True(group.TrySubmitSyllabus());
        var syllabus = Syllabus.CreateSubmitted(Guid.NewGuid(), group.Id);
        var gateConfig = CreateGateConfiguration(ReviewGateType.Syllabus);
        var outbox = new CapturingThesisNotificationOutbox();

        var handler = new ReviewSyllabusHandler(
            new FakeGroupRepository(group),
            new FakeSyllabusRepository(syllabus),
            new FakeGateConfigRepository(gateConfig),
            new FakeSubmissionReviewRepository(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StaffActorId, ["Faculty"], [PermissionCodes.ThesisReviewGatesReview]),
            new FixedTimeProvider(NowUtc),
            outbox,
            new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Hệ thống trợ lý học vụ đa tác tử"));

        var result = await handler.HandleAsync(new ReviewSyllabusCommand(
            group.Id,
            "Rejected",
            "Phạm vi đề tài không đáp ứng chuẩn đầu ra KLTN."));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, outbox.Entries.Count);
        Assert.All(outbox.Entries, e =>
        {
            Assert.Equal("GROUP_STOPPED", e.NotificationType);
            Assert.Equal("Critical", e.Priority);
            Assert.True(e.EnqueueEmail);
        });

        var studentStopped = Assert.Single(outbox.Entries, e => e.ActionPath == "/thesis/proposal");
        Assert.Equal([StudentAId, StudentBId], studentStopped.RecipientUserIds);
        var advisorStopped = Assert.Single(outbox.Entries, e => e.ActionPath == "/topics/advising");
        Assert.Equal([AdvisorId], advisorStopped.RecipientUserIds);
    }

    [Fact]
    public async Task SubmitAndReviewMidtermReport_enqueue_SUBMISSION_CONFIRMED_and_GROUP_STOPPED_SMTP_events()
    {
        var group = CreateTwoStudentGroup();
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());

        var gateConfig = CreateGateConfiguration(ReviewGateType.Midterm);
        var midtermRepo = new FakeMidtermReportRepository();
        var submitOutbox = new CapturingThesisNotificationOutbox();

        var submitHandler = new SubmitMidtermReportHandler(
            new FakeGroupRepository(group),
            midtermRepo,
            new FakeGateConfigRepository(gateConfig),
            new FakeSubmissionFileRepository(),
            new FakeFileValidator(),
            new FakeObjectKeyFactory(),
            new FakeObjectStorage(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StudentAId, ["Student"], []),
            new FixedTimeProvider(NowUtc),
            new FakeUploadLifecycle(),
            new FakeBatchReader(),
            submitOutbox,
            new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Phân tích mã độc Android"));

        await using var stream = new MemoryStream([9, 8, 7]);
        var submitResult = await submitHandler.HandleAsync(new SubmitMidtermReportCommand(group.Id, "giua-ky-v1.pdf", stream));

        Assert.True(submitResult.IsSuccess);
        var confirmed = Assert.Single(submitOutbox.Entries, e => e.NotificationType == "SUBMISSION_CONFIRMED");
        Assert.True(confirmed.EnqueueEmail);
        Assert.Equal("/thesis/midterm", confirmed.ActionPath);

        var reviewOutbox = new CapturingThesisNotificationOutbox();
        var reviewHandler = new ReviewMidtermReportHandler(
            new FakeGroupRepository(group),
            midtermRepo,
            new FakeGateConfigRepository(gateConfig),
            new FakeSubmissionReviewRepository(),
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StaffActorId, ["Faculty"], [PermissionCodes.ThesisReviewGatesReview]),
            new FixedTimeProvider(NowUtc),
            reviewOutbox,
            new FakeTopicSummaryReader(TopicId, BatchId, AdvisorId, "Phân tích mã độc Android"));

        var reviewResult = await reviewHandler.HandleAsync(new ReviewMidtermReportCommand(
            group.Id,
            "Rejected",
            "Không hoàn thành khối lượng tối thiểu giữa kỳ."));

        Assert.True(reviewResult.IsSuccess);
        Assert.Equal(2, reviewOutbox.Entries.Count);
        Assert.All(reviewOutbox.Entries, e =>
        {
            Assert.Equal("GROUP_STOPPED", e.NotificationType);
            Assert.True(e.EnqueueEmail);
        });
    }

    [Fact]
    public async Task RenameRequest_four_step_workflow_enqueues_concise_in_app_notifications()
    {
        var group = CreateTwoStudentGroup();
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());

        var renameRepo = new FakeRenameRequestRepository();
        var topicContract = new FakeTopicRenameContract(TopicId, BatchId, AdvisorId, "Tên đề tài cũ");
        var outbox = new CapturingThesisNotificationOutbox();

        var createHandler = new CreateRenameRequestHandler(
            new FakeGroupRepository(group),
            renameRepo,
            new FakeBatchReader(),
            topicContract,
            new FakeThesisUnitOfWork(),
            new FakeUser(StudentAId, ["Student"], []),
            new FixedTimeProvider(NowUtc),
            outbox);

        var created = await createHandler.HandleAsync(new CreateRenameRequestCommand(
            group.Id,
            "Tên đề tài mới sau mở rộng",
            "New Expanded Thesis Title",
            "Bổ sung thực nghiệm triển khai thực tế."));
        Assert.True(created.IsSuccess);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "RENAME_REQUEST_CREATED" && e.ActionPath == "/topics/rename/review/advisor");

        var advisorHandler = new ReviewRenameRequestByAdvisorHandler(
            renameRepo,
            new FakeGroupRepository(group),
            topicContract,
            new FakeThesisUnitOfWork(),
            new FakeUser(AdvisorId, ["Lecturer"], []),
            new FixedTimeProvider(NowUtc.AddMinutes(5)),
            outbox);
        var advisorApproved = await advisorHandler.HandleAsync(new ReviewRenameRequestByAdvisorCommand(created.Value.Id, "Approve", null));
        Assert.True(advisorApproved.IsSuccess);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "RENAME_REQUEST_ADVISOR_APPROVED" && e.ActionPath == "/topics/rename/review/faculty");

        var facultyHandler = new ReviewRenameRequestByFacultyHandler(
            renameRepo,
            new FakeThesisUnitOfWork(),
            new FakeUser(StaffActorId, ["Faculty"], [PermissionCodes.ThesisReviewGatesReview]),
            new FixedTimeProvider(NowUtc.AddMinutes(10)),
            new FakeGroupRepository(group),
            topicContract,
            outbox);
        var facultyApproved = await facultyHandler.HandleAsync(new ReviewRenameRequestByFacultyCommand(created.Value.Id, "Approve", null));
        Assert.True(facultyApproved.IsSuccess);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "RENAME_REQUEST_FACULTY_APPROVED" && e.ActionPath == "/topics/rename/review/academic-affairs");

        var aaHandler = new ReviewRenameRequestByAcademicAffairsHandler(
            renameRepo,
            new FakeGroupRepository(group),
            topicContract,
            new FakeThesisAuditOutbox(),
            new FakeThesisUnitOfWork(),
            new FakeUser(StaffActorId, ["AcademicAffairs"], []),
            new FixedTimeProvider(NowUtc.AddMinutes(15)),
            new PassThroughRenameCoordinator(),
            outbox);
        var aaApproved = await aaHandler.HandleAsync(new ReviewRenameRequestByAcademicAffairsCommand(created.Value.Id, "Approve", null));
        Assert.True(aaApproved.IsSuccess);
        Assert.True(group.AllowSyllabusResubmission);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "RENAME_REQUEST_APPROVED" && e.ActionPath == "/thesis/proposal");
        Assert.All(outbox.Entries, e => Assert.False(e.EnqueueEmail));
    }

    private static StudentGroup CreateTwoStudentGroup()
    {
        var group = new StudentGroup(Guid.NewGuid(), TopicId, NowUtc.AddDays(-5));
        group.AddConfirmedMember(StudentAId, BatchId, NowUtc.AddDays(-5));
        group.AddConfirmedMember(StudentBId, BatchId, NowUtc.AddDays(-4));
        return group;
    }

    private static ReviewGateConfiguration CreateGateConfiguration(ReviewGateType gateType)
        => new(
            Guid.NewGuid(),
            BatchId,
            gateType,
            NowUtc.AddDays(3),
            NowUtc.AddDays(5),
            NowUtc.AddDays(7),
            StaffActorId,
            NowUtc.AddDays(-10));

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

    private sealed class FakeSyllabusRepository(Syllabus? existing = null) : ISyllabusRepository
    {
        private Syllabus? _stored = existing;
        public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.GroupId == groupId ? _stored : null);
        public void Add(Syllabus syllabus) => _stored = syllabus;
    }

    private sealed class FakeMidtermReportRepository(MidtermReport? existing = null) : IMidtermReportRepository
    {
        private MidtermReport? _stored = existing;
        public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.GroupId == groupId ? _stored : null);
        public void Add(MidtermReport midtermReport) => _stored = midtermReport;
    }

    private sealed class FakeGateConfigRepository(ReviewGateConfiguration config) : IReviewGateConfigurationRepository
    {
        public Task<ReviewGateConfiguration?> GetByBatchAndGateAsync(Guid batchId, ReviewGateType gateType, CancellationToken cancellationToken = default)
            => Task.FromResult(config.BatchId == batchId && config.GateType == gateType ? config : null);
        public Task<IReadOnlyList<ReviewGateConfiguration>> GetByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewGateConfiguration>>([config]);
        public void Add(ReviewGateConfiguration configuration) { }
    }

    private sealed class FakeSubmissionFileRepository : ISubmissionFileRepository
    {
        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<SubmissionFile?>(null);
        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionFile>>([]);
        public void Add(SubmissionFile submissionFile) { }
    }

    private sealed class FakeSubmissionReviewRepository : ISubmissionReviewRepository
    {
        public Task<IReadOnlyList<SubmissionReview>> GetBySubmissionAsync(ReviewGateType gateType, Guid submissionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionReview>>([]);
        public void Add(SubmissionReview review) { }
    }

    private sealed class FakeRenameRequestRepository : IRenameRequestRepository
    {
        private RenameRequest? _stored;
        public void Add(RenameRequest request) => _stored = request;
        public Task<RenameRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<RenameRequest?> LockByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_stored?.Id == id ? _stored : null);
        public Task<IReadOnlyList<RenameRequest>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RenameRequest>>(_stored is null ? [] : [_stored]);
        public Task<bool> HasActiveRequestForGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<IReadOnlyList<RenameRequest>> ListAsync(Guid? groupId, RenameRequestStatus? status, IReadOnlyCollection<Guid>? topicIdsFilter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RenameRequest>>(_stored is null ? [] : [_stored]);
    }

    private sealed class FakeFileValidator : IThesisSubmissionFileValidator
    {
        public Task<ValidatedThesisSubmissionFile> ValidateAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidatedThesisSubmissionFile(
                fileName,
                ".pdf",
                "application/pdf",
                245_760,
                new string('a', 64),
                new byte[] { 1, 2, 3, 4 }));
    }

    private sealed class FakeObjectKeyFactory : IThesisSubmissionObjectKeyFactory
    {
        public string Create(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension)
            => $"{entityType}/{entityId}/v{versionNo}{fileExtension}";
        public string CreateAttachment(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension)
            => $"{entityType}/{entityId}/att-v{versionNo}{fileExtension}";
    }

    private sealed class FakeObjectStorage : IThesisSubmissionObjectStorage
    {
        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
            => Task.FromResult(new Uri("https://example.test/download"));
        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUploadLifecycle : IThesisUploadLifecycle
    {
        public Task<Guid> BeginAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
        public Task<Error?> SaveAsync(Guid attemptId, Func<CancellationToken, Task<Error?>> save, Error expiredError, CancellationToken cancellationToken = default)
            => save(cancellationToken);
        public Task FinishAsync(Guid attemptId) => Task.CompletedTask;
    }

    private sealed class FakeBatchReader : IKltnBatchReader
    {
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchRegistrationWindowContractDto?>(new(batchId, "Open", true, nowUtc.AddDays(5)));
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchTopicPreparationWindowContractDto?>(new(batchId, "Open", true, nowUtc.AddDays(5)));
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchRenameRequestWindowContractDto?>(new(batchId, "Open", true, DateOnly.FromDateTime(nowUtc.AddDays(-5)), nowUtc.AddDays(10)));
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchFinalReportSubmissionWindowContractDto?>(new(batchId, "Open", true, nowUtc.AddDays(-2), nowUtc.AddDays(10)));
        public Task<BatchReviewGateStageWindowContractDto?> GetSyllabusSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchReviewGateStageWindowContractDto?>(new(batchId, "Open", true, DateOnly.FromDateTime(nowUtc.AddDays(-2)), nowUtc.AddDays(10)));
        public Task<BatchReviewGateStageWindowContractDto?> GetMidtermSubmissionWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<BatchReviewGateStageWindowContractDto?>(new(batchId, "Open", true, DateOnly.FromDateTime(nowUtc.AddDays(-2)), nowUtc.AddDays(10)));
    }

    private sealed class FakeTopicSummaryReader(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : ITopicSummaryReader
    {
        public Task<IReadOnlyDictionary<Guid, TopicSummaryContractDto>> GetByIdsAsync(IEnumerable<Guid> topicIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, TopicSummaryContractDto> dict = new Dictionary<Guid, TopicSummaryContractDto>
            {
                [topicId] = new(topicId, batchId, advisorId, titleVi, null)
            };
            return Task.FromResult(dict);
        }
    }

    private sealed class FakeStudentInfoReader(Guid studentId, string fullName) : IStudentInfoReader
    {
        public Task<IReadOnlyDictionary<Guid, StudentInfoContractDto>> GetStudentsByIdsAsync(IEnumerable<Guid> studentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, StudentInfoContractDto>>(new Dictionary<Guid, StudentInfoContractDto>());
        public Task<StudentInfoContractDto?> GetStudentByIdOrCodeAsync(Guid? id, string? userCode, CancellationToken cancellationToken = default)
            => Task.FromResult(id == studentId ? new StudentInfoContractDto(studentId, "sv@gm.uit.edu.vn", fullName, "22520001", null) : null);
    }

    private sealed class FakeTopicRenameContract(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : ITopicRenameContract
    {
        private string _titleVi = titleVi;
        public Task<TopicTitleSnapshotContractDto?> GetTopicSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == topicId ? new TopicTitleSnapshotContractDto(topicId, batchId, advisorId, _titleVi, null) : null);
        public Task<TopicTitleUpdateContractResultDto> UpdateTopicTitlesAsync(Guid id, string newTitleVi, string? newTitleEn, CancellationToken cancellationToken = default)
        {
            var old = _titleVi;
            _titleVi = newTitleVi;
            return Task.FromResult(TopicTitleUpdateContractResultDto.Success(old, newTitleVi, null, newTitleEn));
        }
    }

    private sealed class PassThroughRenameCoordinator : IRenameRequestTransactionCoordinator
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> workflow, CancellationToken cancellationToken = default)
            => workflow(cancellationToken);
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

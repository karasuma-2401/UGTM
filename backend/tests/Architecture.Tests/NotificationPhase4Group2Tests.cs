using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Notifications;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.TopicCatalog.Application.Notifications;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.ReviewTopic;
using UGTM.Modules.TopicCatalog.Application.Topics.SubmitTopic;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase4Group2Tests
{
    private static readonly Guid ActorId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid AdvisorId = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    private static readonly DateTime OccurredAtUtc = new(2026, 11, 25, 16, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly BatchStart = new(2026, 9, 7);

    [Fact]
    public async Task OpenKltnBatchHandler_enqueues_BATCH_OPENED_in_app_notification()
    {
        var batch = CreateDraftBatch();
        var scoreConfig = CreateActiveScoreConfig(batch.Id);
        var notifOutbox = new CapturingAcademicNotificationOutbox();

        var handler = new OpenKltnBatchHandler(
            new FakeBatchRepository(batch),
            new FakeScoreConfigRepository(scoreConfig),
            new FakeAcademicAuditOutbox(),
            new FakeAcademicUnitOfWork(),
            new FakeUser(ActorId, roles: [], permissions: [PermissionCodes.AcademicBatchesManage]),
            new FixedTimeProvider(OccurredAtUtc),
            notifOutbox);

        var result = await handler.HandleAsync(new OpenKltnBatchCommand(batch.Id));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(notifOutbox.Entries);
        Assert.Equal("BATCH_OPENED", entry.NotificationType);
        Assert.Equal("Normal", entry.Priority);
        Assert.False(entry.EnqueueEmail);
        Assert.True(entry.EnqueuePush);
        Assert.Equal(["Student", "Lecturer", "Faculty", "AcademicAffairs"], entry.RecipientRoles);
        Assert.Equal([ActorId], entry.ExcludeUserIds);
        Assert.Equal("/dashboard", entry.ActionPath);
        Assert.Equal(batch.Id, entry.AcademicBatchId);
    }

    [Fact]
    public async Task UpdateKltnBatchScheduleHandler_enqueues_notification_only_when_batch_is_open()
    {
        var draftBatch = CreateDraftBatch();
        var draftOutbox = new CapturingAcademicNotificationOutbox();
        var draftHandler = new UpdateKltnBatchScheduleHandler(
            new FakeBatchRepository(draftBatch),
            new FakeAcademicAuditOutbox(),
            new FakeAcademicUnitOfWork(),
            new FakeUser(ActorId, roles: [], permissions: [PermissionCodes.AcademicBatchesManage]),
            new FixedTimeProvider(OccurredAtUtc.AddMinutes(5)),
            draftOutbox);

        var draftItems = draftBatch.Stages
            .OrderBy(s => s.OrderIndex)
            .Select(s => new UpdateStageScheduleItem(
                s.OrderIndex,
                s.StageCode,
                s.StageName,
                s.StartDate,
                s.EndDate,
                new DateTimeOffset(s.DeadlineAtUtc, TimeSpan.Zero)))
            .ToArray();

        var draftResult = await draftHandler.HandleAsync(new UpdateKltnBatchScheduleCommand(
            draftBatch.Id,
            draftItems,
            "Tinh chỉnh nháp"));

        Assert.True(draftResult.IsSuccess);
        Assert.Empty(draftOutbox.Entries);

        var openBatch = CreateDraftBatch();
        openBatch.Open(CreateActiveScoreConfig(openBatch.Id), ActorId, OccurredAtUtc);
        var openOutbox = new CapturingAcademicNotificationOutbox();
        var openHandler = new UpdateKltnBatchScheduleHandler(
            new FakeBatchRepository(openBatch),
            new FakeAcademicAuditOutbox(),
            new FakeAcademicUnitOfWork(),
            new FakeUser(ActorId, roles: [], permissions: [PermissionCodes.AcademicBatchesManage]),
            new FixedTimeProvider(OccurredAtUtc.AddMinutes(10)),
            openOutbox);

        var openItems = openBatch.Stages
            .OrderBy(s => s.OrderIndex)
            .Select(s => new UpdateStageScheduleItem(
                s.OrderIndex,
                s.StageCode,
                s.StageName,
                s.StartDate,
                s.EndDate,
                new DateTimeOffset(s.DeadlineAtUtc, TimeSpan.Zero)))
            .ToArray();

        var openResult = await openHandler.HandleAsync(new UpdateKltnBatchScheduleCommand(
            openBatch.Id,
            openItems,
            "Gia hạn nộp đề cương 2 ngày"));

        Assert.True(openResult.IsSuccess);
        var entry = Assert.Single(openOutbox.Entries);
        Assert.Equal("BATCH_SCHEDULE_UPDATED", entry.NotificationType);
        Assert.False(entry.EnqueueEmail);
        Assert.Equal("/dashboard", entry.ActionPath);
    }

    [Fact]
    public async Task SubmitTopicHandler_enqueues_TOPIC_SUBMITTED_for_Faculty()
    {
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            AdvisorId = AdvisorId,
            TitleVi = "Xây dựng hệ thống quản lý khóa luận thông minh",
            StudentNumber = 2,
            Status = TopicStatus.Draft,
            CreatedAt = OccurredAtUtc
        };
        var outbox = new CapturingTopicNotificationOutbox();
        var handler = new SubmitTopicHandler(
            new FakeTopicRepository(topic),
            new FakeTopicUnitOfWork(),
            new FakeUser(AdvisorId, roles: ["Lecturer"], permissions: []),
            outbox,
            new FixedTimeProvider(OccurredAtUtc));

        var result = await handler.HandleAsync(new SubmitTopicCommand(topic.Id));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(outbox.Entries);
        Assert.Equal("TOPIC_SUBMITTED", entry.NotificationType);
        Assert.Equal("Normal", entry.Priority);
        Assert.False(entry.EnqueueEmail);
        Assert.Equal(["Faculty"], entry.RecipientRoles);
        Assert.Equal([AdvisorId], entry.ExcludeUserIds);
        Assert.Equal("/topics/review", entry.ActionPath);
    }

    [Fact]
    public async Task ReviewTopicHandler_NeedRevision_enqueues_critical_SMTP_TOPIC_REVISION_REQUESTED()
    {
        var batchId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            AdvisorId = AdvisorId,
            TitleVi = "Ứng dụng học sâu nhận diện văn bản Hán Nôm",
            StudentNumber = 2,
            Status = TopicStatus.Submitted,
            CreatedAt = OccurredAtUtc
        };
        var outbox = new CapturingTopicNotificationOutbox();
        var handler = new ReviewTopicHandler(
            new FakeTopicRepository(topic),
            new FakeTopicUnitOfWork(),
            new FakeUser(ActorId, roles: ["Faculty"], permissions: []),
            outbox,
            new FakeBatchSummaryReader(batchId, "KLTN HK1 2026-2027"),
            new FixedTimeProvider(OccurredAtUtc));

        var result = await handler.HandleAsync(new ReviewTopicCommand(
            topic.Id,
            "NeedRevision",
            "Cần làm rõ bộ dữ liệu thực nghiệm và tiêu chí đánh giá độ chính xác."));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(outbox.Entries);
        Assert.Equal("TOPIC_REVISION_REQUESTED", entry.NotificationType);
        Assert.Equal("Critical", entry.Priority);
        Assert.True(entry.EnqueueEmail);
        Assert.Equal([AdvisorId], entry.RecipientUserIds);
        Assert.Equal("/topics/propose", entry.ActionPath);
        Assert.NotNull(entry.EmailMetadata);
        Assert.Contains(entry.EmailMetadata!, m => m.Label == "Đợt KLTN" && m.Value == "KLTN HK1 2026-2027");
        Assert.Contains(entry.EmailMetadata!, m => m.Label == "Góp ý từ Khoa" && m.Value.Contains("bộ dữ liệu thực nghiệm"));
    }

    [Theory]
    [InlineData("Approved", "TOPIC_APPROVED", null)]
    [InlineData("Rejected", "TOPIC_REJECTED", "Trùng hướng nghiên cứu đã triển khai kỳ trước.")]
    public async Task ReviewTopicHandler_Approved_or_Rejected_enqueues_in_app_only_notification(
        string decision,
        string expectedType,
        string? reviewNote)
    {
        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            AdvisorId = AdvisorId,
            TitleVi = "Phát hiện bất thường giao dịch tài chính thời gian thực",
            StudentNumber = 2,
            Status = TopicStatus.Submitted,
            CreatedAt = OccurredAtUtc
        };
        var outbox = new CapturingTopicNotificationOutbox();
        var handler = new ReviewTopicHandler(
            new FakeTopicRepository(topic),
            new FakeTopicUnitOfWork(),
            new FakeUser(ActorId, roles: ["Faculty"], permissions: []),
            outbox,
            timeProvider: new FixedTimeProvider(OccurredAtUtc));

        var result = await handler.HandleAsync(new ReviewTopicCommand(topic.Id, decision, reviewNote));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(outbox.Entries);
        Assert.Equal(expectedType, entry.NotificationType);
        Assert.Equal("Normal", entry.Priority);
        Assert.False(entry.EnqueueEmail);
        Assert.Equal([AdvisorId], entry.RecipientUserIds);
        Assert.Equal("/topics/advising", entry.ActionPath);
    }

    private static KltnBatch CreateDraftBatch()
    {
        var template = ScheduleTemplateVersion.CreateDraft(
            Guid.NewGuid(),
            "UIT_KLTN_STANDARD",
            1,
            "Standard schedule",
            KltnBatch.DefaultTimeZoneId,
            [
                new StageTemplateInput(1, "TOPIC_PREPARATION", "TOPIC_PREPARATION", 0, 6, new TimeOnly(23, 59)),
                new StageTemplateInput(2, "REGISTRATION_AND_SYLLABUS", "REGISTRATION_AND_SYLLABUS", 7, 13, new TimeOnly(23, 59)),
                new StageTemplateInput(3, "PROGRESS_EXECUTION", "PROGRESS_EXECUTION", 14, 62, new TimeOnly(23, 59)),
                new StageTemplateInput(4, "MIDTERM_GATE", "MIDTERM_GATE", 63, 69, new TimeOnly(23, 59)),
                new StageTemplateInput(5, "FINAL_EVALUATION", "FINAL_EVALUATION", 70, 125, new TimeOnly(23, 59)),
                new StageTemplateInput(6, "DEFENSE", "DEFENSE", 126, 139, new TimeOnly(23, 59)),
                new StageTemplateInput(7, "ARCHIVAL_AND_PUBLICATION", "ARCHIVAL_AND_PUBLICATION", 140, 146, new TimeOnly(23, 59))
            ],
            ActorId,
            OccurredAtUtc);
        template.Publish(ActorId, OccurredAtUtc);

        return KltnBatch.Create(
            Guid.NewGuid(),
            "KLTN HK1 2026-2027",
            "2026-2027",
            1,
            BatchStart,
            BatchStart.AddDays(146),
            template,
            ActorId,
            OccurredAtUtc);
    }

    private static ScoreWeightConfig CreateActiveScoreConfig(Guid batchId)
    {
        var config = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            batchId,
            1,
            ScoreCalculationMethod.IntegerCoefficient,
            [new ScoreComponentDefinition(
                "COUNCIL",
                "Council",
                ScoreComponentSource.CouncilAverage,
                ScoreComponentApplicability.Always,
                1m,
                true,
                null,
                false,
                1)],
            ActorId,
            OccurredAtUtc);
        config.Publish(ActorId, OccurredAtUtc);
        return config;
    }

    private sealed class CapturingAcademicNotificationOutbox : IAcademicPeriodsNotificationOutbox
    {
        public List<NotificationOutboxEntry> Entries { get; } = [];
        public void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class CapturingTopicNotificationOutbox : ITopicCatalogNotificationOutbox
    {
        public List<NotificationOutboxEntry> Entries { get; } = [];
        public void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeAcademicAuditOutbox : IAcademicPeriodsAuditOutbox
    {
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) { }
    }

    private sealed class FakeAcademicUnitOfWork : IAcademicPeriodsUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeBatchRepository(KltnBatch batch) : IKltnBatchRepository
    {
        public Task<KltnBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(batch.Id == id ? batch : null);
        public Task ReloadAsync(KltnBatch value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<bool> ExistsAsync(string academicYear, int semester, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task<bool> ExistsAsync(string academicYear, int semester, Guid excludeBatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task AcquireOpenBatchLockAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task<bool> ExistsOpenBatchAsync(Guid excludeBatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public void Add(KltnBatch value) { }
        public void AddStages(IEnumerable<Stage> stages) { }
    }

    private sealed class FakeScoreConfigRepository(ScoreWeightConfig active) : IScoreConfigurationRepository
    {
        public Task<ScoreWeightConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(active.Id == id ? active : null);
        public Task<ScoreWeightConfig?> GetActiveByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(active.BatchId == batchId ? active : null);
        public Task<ScoreWeightConfig?> GetDraftByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ScoreWeightConfig?>(null);
        public Task<int> GetLatestVersionNoAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(active.VersionNo);
        public Task ReloadAsync(ScoreWeightConfig configuration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public void Add(ScoreWeightConfig configuration) { }
        public void AddComponents(IEnumerable<ScoreComponentConfig> components) { }
    }

    private sealed class FakeTopicRepository(Topic topic) : ITopicRepository
    {
        public Task<Topic?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(topic.Id == id ? topic : null);
        public Task<IReadOnlyList<Topic>> GetByBatchAsync(Guid batchId, IReadOnlyCollection<Guid>? topicIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Topic>>([topic]);
        public Task<bool> ExistsByAdvisorAndTitleAsync(Guid batchId, Guid advisorId, string titleVi, Guid? excludeTopicId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public void Add(Topic value) { }
        public void Remove(Topic value) { }
    }

    private sealed class FakeTopicUnitOfWork : ITopicUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeBatchSummaryReader(Guid batchId, string batchName) : IBatchSummaryReader
    {
        public Task<IReadOnlyDictionary<Guid, BatchSummaryContractDto>> GetByIdsAsync(
            IEnumerable<Guid> batchIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, BatchSummaryContractDto> dict = new Dictionary<Guid, BatchSummaryContractDto>
            {
                [batchId] = new(batchId, batchName)
            };
            return Task.FromResult(dict);
        }
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

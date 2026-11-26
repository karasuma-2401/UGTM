using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UGTM.Api.Modules;
using UGTM.Modules.AcademicPeriods.Contracts.Reminders;
using UGTM.Modules.DefenseCouncil.Contracts.Reminders;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.Notifications.Infrastructure.Reminders;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;
using UGTM.Modules.TopicCatalog.Contracts.Reminders;
using Xunit;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase5ReminderTests
{
    [Fact]
    public async Task Coordinator_Sends_Personalized_Student_Reminders_At_48h_And_24h_And_Skips_Completed_Groups()
    {
        var nowUtc = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
        var batchId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var advisorId = Guid.NewGuid();
        var pendingGroupId = Guid.NewGuid();
        var registeredGroupId = Guid.NewGuid();
        var completedGroupId = Guid.NewGuid();
        var student1 = Guid.NewGuid();
        var student2 = Guid.NewGuid();
        var studentDone = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var academicReader = new StubAcademicReader([
            new OpenBatchReminderSnapshotDto(
                batchId,
                "KLTN HK1 2026-2027",
                "Asia/Ho_Chi_Minh",
                [
                    new OpenBatchStageReminderDto(5, "FINAL_EVALUATION", "Đánh giá & Nộp cuối kỳ", nowUtc.AddHours(40)),
                    new OpenBatchStageReminderDto(7, "ARCHIVAL_AND_PUBLICATION", "Lưu chiểu & Công bố", nowUtc.AddHours(18))
                ])
        ]);

        var topicReader = new StubTopicReader(new TopicBatchReminderSnapshotDto(
            [],
            [],
            new Dictionary<Guid, TopicReminderSummaryDto>
            {
                [topicId] = new TopicReminderSummaryDto(topicId, advisorId, "Hệ thống quản lý khóa luận UGTM")
            }));

        var thesisReader = new StubThesisReader(new ThesisBatchReminderSnapshotDto(
            [
                new ReviewGateReminderDto(
                    "Syllabus",
                    nowUtc.AddHours(22),
                    nowUtc.AddHours(60),
                    nowUtc.AddHours(12))
            ],
            [
                new ThesisGroupReminderDto(
                    registeredGroupId,
                    topicId,
                    "Registered",
                    [student1],
                    SyllabusStatus: null,
                    MidtermStatus: null,
                    HasInitialFinalReport: false,
                    HasRevisedFinalReport: false,
                    ArchivalStatus: null),
                new ThesisGroupReminderDto(
                    pendingGroupId,
                    topicId,
                    "InProgress",
                    [student1, student2],
                    SyllabusStatus: "Approved",
                    MidtermStatus: "Approved",
                    HasInitialFinalReport: false,
                    HasRevisedFinalReport: false,
                    ArchivalStatus: null),
                new ThesisGroupReminderDto(
                    completedGroupId,
                    topicId,
                    "InProgress",
                    [studentDone],
                    SyllabusStatus: "Approved",
                    MidtermStatus: "Approved",
                    HasInitialFinalReport: true,
                    HasRevisedFinalReport: true,
                    ArchivalStatus: "Approved")
            ],
            [
                new MilestoneReminderDto(
                    milestoneId,
                    pendingGroupId,
                    "Báo cáo tiến độ tuần 6",
                    nowUtc.AddHours(23),
                    LatestReportStatus: null)
            ]));

        var councilReader = new StubCouncilReader(new DefenseCouncilBatchReminderSnapshotDto(
            new HashSet<Guid>(),
            [],
            new HashSet<Guid>(),
            [
                new ActiveCouncilReminderDto(
                    Guid.NewGuid(),
                    "HD-01",
                    DateOnly.FromDateTime(nowUtc.AddDays(-2)),
                    "08:00 - 11:30",
                    "B1.02",
                    [advisorId],
                    [advisorId],
                    [pendingGroupId, completedGroupId],
                    [])
            ],
            new HashSet<Guid>()));

        var recipientDirectory = new StubRecipientDirectory(
            new Dictionary<Guid, NotificationRecipientContractDto>
            {
                [student1] = new NotificationRecipientContractDto(student1, "Nguyễn Văn A", "a@uit.edu.vn"),
                [student2] = new NotificationRecipientContractDto(student2, "Trần Thị B", "b@uit.edu.vn"),
                [studentDone] = new NotificationRecipientContractDto(studentDone, "Lê Văn C", "c@uit.edu.vn")
            });

        var ingestion = new RecordingIngestionService();
        var coordinator = CreateCoordinator(
            academicReader,
            topicReader,
            thesisReader,
            councilReader,
            recipientDirectory,
            ingestion,
            nowUtc);

        var created = await coordinator.RunOnceAsync();

        // Pending group has 2 students x 6 reminders (Stage2 48h, SyllabusInitial 24h, SyllabusFinal 24h, Milestone 24h, InitialFinal 24h, RevisedFinal+Archival 24h = 7? Wait: SyllabusFinal 24h + InitialFinal 24h + RevisedFinal 24h + Archival 24h).
        // Specifically: no reminders for studentDone!
        Assert.True(created > 0);
        Assert.DoesNotContain(ingestion.Requests, r => r.RecipientId == studentDone);
        Assert.Contains(ingestion.Requests, r =>
            r.RecipientId == student1 &&
            r.NotificationType == "SYLLABUS_INITIAL_DEADLINE_SOON" &&
            r.EnqueueEmail &&
            r.DeduplicationKey!.EndsWith(":24h", StringComparison.Ordinal));
        Assert.Contains(ingestion.Requests, r =>
            r.RecipientId == student2 &&
            r.NotificationType == "MILESTONE_DEADLINE_SOON" &&
            r.ResourceId == milestoneId &&
            r.DeduplicationKey!.EndsWith(":24h", StringComparison.Ordinal));
        Assert.Contains(ingestion.Requests, r =>
            r.RecipientId == student1 &&
            r.NotificationType == "FINAL_REPORT_DEADLINE_SOON" &&
            r.DeduplicationKey!.EndsWith(":48h", StringComparison.Ordinal));
        Assert.Contains(ingestion.Requests, r =>
            r.RecipientId == student1 &&
            r.NotificationType == "ARCHIVAL_SUBMISSION_DEADLINE_SOON" &&
            r.DeduplicationKey!.EndsWith(":24h", StringComparison.Ordinal));

        // Running again at the same timestamp must be deduplicated cleanly.
        var secondRun = await coordinator.RunOnceAsync();
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task Coordinator_Aggregates_All_Staff_24h_Items_Into_Single_Digest_Email_Per_User()
    {
        var nowUtc = new DateTime(2026, 12, 2, 7, 0, 0, DateTimeKind.Utc);
        var batchId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var dualStaffId = Guid.NewGuid();
        var facultyOnlyId = Guid.NewGuid();

        var academicReader = new StubAcademicReader([
            new OpenBatchReminderSnapshotDto(
                batchId,
                "KLTN HK1 2026-2027",
                "Asia/Ho_Chi_Minh",
                [
                    new OpenBatchStageReminderDto(1, "TOPIC_PREPARATION", "Chuẩn bị & Duyệt đề tài", nowUtc.AddHours(10)),
                    new OpenBatchStageReminderDto(5, "FINAL_EVALUATION", "Đánh giá cuối kỳ", nowUtc.AddHours(11)),
                    new OpenBatchStageReminderDto(6, "DEFENSE", "Đánh giá & Bảo vệ hội đồng", nowUtc.AddHours(12)),
                    new OpenBatchStageReminderDto(7, "ARCHIVAL_AND_PUBLICATION", "Lưu chiểu & Công bố", nowUtc.AddHours(14))
                ])
        ]);

        var topicReader = new StubTopicReader(new TopicBatchReminderSnapshotDto(
            [new DraftTopicReminderDto(topicId, dualStaffId, "Đề tài AI trong y tế")],
            [new SubmittedTopicReminderDto(Guid.NewGuid(), dualStaffId, "Đề tài Blockchain")],
            new Dictionary<Guid, TopicReminderSummaryDto>
            {
                [topicId] = new TopicReminderSummaryDto(topicId, dualStaffId, "Đề tài AI trong y tế")
            }));

        var thesisReader = new StubThesisReader(new ThesisBatchReminderSnapshotDto(
            [
                new ReviewGateReminderDto(
                    "Syllabus",
                    nowUtc.AddHours(-24),
                    nowUtc.AddHours(16),
                    nowUtc.AddHours(48))
            ],
            [
                new ThesisGroupReminderDto(
                    groupId,
                    topicId,
                    "InProgress",
                    [],
                    SyllabusStatus: "Submitted",
                    MidtermStatus: null,
                    HasInitialFinalReport: true,
                    HasRevisedFinalReport: true,
                    ArchivalStatus: "Submitted")
            ],
            []));

        var councilReader = new StubCouncilReader(new DefenseCouncilBatchReminderSnapshotDto(
            new HashSet<Guid>(),
            [new PendingReviewerAssignmentReminderDto(Guid.NewGuid(), groupId, dualStaffId)],
            new HashSet<Guid>(),
            [
                new ActiveCouncilReminderDto(
                    Guid.NewGuid(),
                    "HD-01",
                    DateOnly.FromDateTime(nowUtc.AddHours(18)),
                    "08:00 - 11:30",
                    "B1.02",
                    [dualStaffId],
                    [dualStaffId],
                    [groupId],
                    [groupId])
            ],
            new HashSet<Guid>()));

        var recipientDirectory = new StubRecipientDirectory(
            new Dictionary<Guid, NotificationRecipientContractDto>
            {
                [dualStaffId] = new NotificationRecipientContractDto(dualStaffId, "TS. Nguyễn Giảng Viên", "gv@uit.edu.vn"),
                [facultyOnlyId] = new NotificationRecipientContractDto(facultyOnlyId, "Cô Giáo Vụ", "vpk@uit.edu.vn")
            },
            facultyIds: [facultyOnlyId],
            affairsIds: [facultyOnlyId]);

        var ingestion = new RecordingIngestionService();
        var coordinator = CreateCoordinator(
            academicReader,
            topicReader,
            thesisReader,
            councilReader,
            recipientDirectory,
            ingestion,
            nowUtc);

        var created = await coordinator.RunOnceAsync();

        Assert.Equal(2, created);

        var dualDigest = Assert.Single(ingestion.Requests, r => r.RecipientId == dualStaffId);
        Assert.Equal("STAFF_DEADLINE_DIGEST_24H", dualDigest.NotificationType);
        Assert.True(dualDigest.EnqueueEmail);
        Assert.Equal($"REMINDER:STAFF_DIGEST:{batchId:N}:20261202", dualDigest.DeduplicationKey);
        Assert.NotNull(dualDigest.EmailDigestSections);
        // Draft topic + Advisor eval + Reviewer assignment + Reviewer eval + Defense council + Council minutes
        Assert.True(dualDigest.EmailDigestSections!.Count >= 5);

        var facultyDigest = Assert.Single(ingestion.Requests, r => r.RecipientId == facultyOnlyId);
        Assert.Equal("STAFF_DEADLINE_DIGEST_24H", facultyDigest.NotificationType);
        Assert.NotNull(facultyDigest.EmailDigestSections);
        // Topic approval + Syllabus faculty response + Archival review + Score publication
        Assert.True(facultyDigest.EmailDigestSections!.Count >= 3);
    }

    [Fact]
    public void AddUgtmModules_Registers_DeadlineReminderCoordinator_And_All_Four_Snapshot_Readers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=ugtm_test;Username=ugtm;Password=ugtm",
                ["Notifications:Reminders:Enabled"] = "true",
                ["Notifications:Reminders:ScanIntervalMinutes"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUgtmModules(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDeadlineReminderCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAcademicDeadlineSnapshotReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITopicDeadlineSnapshotReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IThesisDeadlineSnapshotReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDefenseCouncilDeadlineSnapshotReader>());
    }

    private static DeadlineReminderCoordinator CreateCoordinator(
        IAcademicDeadlineSnapshotReader academicReader,
        ITopicDeadlineSnapshotReader topicReader,
        IThesisDeadlineSnapshotReader thesisReader,
        IDefenseCouncilDeadlineSnapshotReader councilReader,
        INotificationRecipientDirectory recipientDirectory,
        INotificationIngestionService ingestionService,
        DateTime nowUtc)
    {
        return new DeadlineReminderCoordinator(
            academicReader,
            topicReader,
            thesisReader,
            councilReader,
            recipientDirectory,
            ingestionService,
            Options.Create(new DeadlineReminderOptions
            {
                Enabled = true,
                ScanIntervalMinutes = 15,
                StudentLeadTimeHours = [48, 24],
                StaffLeadTimeHours = [24]
            }),
            new FixedTimeProvider(nowUtc),
            NullLogger<DeadlineReminderCoordinator>.Instance);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class StubAcademicReader(IReadOnlyList<OpenBatchReminderSnapshotDto> batches) : IAcademicDeadlineSnapshotReader
    {
        public Task<IReadOnlyList<OpenBatchReminderSnapshotDto>> GetOpenBatchesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(batches);
    }

    private sealed class StubTopicReader(TopicBatchReminderSnapshotDto snapshot) : ITopicDeadlineSnapshotReader
    {
        public Task<TopicBatchReminderSnapshotDto> GetBatchSnapshotAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class StubThesisReader(ThesisBatchReminderSnapshotDto snapshot) : IThesisDeadlineSnapshotReader
    {
        public Task<ThesisBatchReminderSnapshotDto> GetBatchSnapshotAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class StubCouncilReader(DefenseCouncilBatchReminderSnapshotDto snapshot) : IDefenseCouncilDeadlineSnapshotReader
    {
        public Task<DefenseCouncilBatchReminderSnapshotDto> GetBatchSnapshotAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class StubRecipientDirectory(
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> users,
        IReadOnlyList<Guid>? facultyIds = null,
        IReadOnlyList<Guid>? affairsIds = null) : INotificationRecipientDirectory
    {
        public Task<IReadOnlyList<NotificationRecipientContractDto>> ResolveActiveRecipientsAsync(
            IReadOnlyCollection<Guid>? userIds = null,
            IReadOnlyCollection<string>? roleNames = null,
            IReadOnlyCollection<string>? permissionCodes = null,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, NotificationRecipientContractDto>();

            if (userIds is not null)
            {
                foreach (var id in userIds)
                {
                    if (users.TryGetValue(id, out var user))
                    {
                        result[id] = user;
                    }
                }
            }

            if (roleNames is not null)
            {
                if (roleNames.Contains("Faculty", StringComparer.OrdinalIgnoreCase) && facultyIds is not null)
                {
                    foreach (var id in facultyIds)
                    {
                        if (users.TryGetValue(id, out var user))
                        {
                            result[id] = user;
                        }
                    }
                }

                if (roleNames.Contains("AcademicAffairs", StringComparer.OrdinalIgnoreCase) && affairsIds is not null)
                {
                    foreach (var id in affairsIds)
                    {
                        if (users.TryGetValue(id, out var user))
                        {
                            result[id] = user;
                        }
                    }
                }
            }

            if (permissionCodes is not null && permissionCodes.Count > 0 && facultyIds is not null)
            {
                foreach (var id in facultyIds)
                {
                    if (users.TryGetValue(id, out var user))
                    {
                        result[id] = user;
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<NotificationRecipientContractDto>>(result.Values.ToArray());
        }
    }

    private sealed class RecordingIngestionService : INotificationIngestionService
    {
        private readonly HashSet<(Guid RecipientId, string Key)> _seenKeys = new();
        public List<NotificationIngestionRequest> Requests { get; } = [];

        public Task<NotificationIngestionResult> IngestAsync(
            NotificationIngestionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(request.DeduplicationKey) &&
                !_seenKeys.Add((request.RecipientId, request.DeduplicationKey)))
            {
                return Task.FromResult(new NotificationIngestionResult(null, Deduplicated: true, DeliveriesEnqueued: 0));
            }

            Requests.Add(request);
            return Task.FromResult(new NotificationIngestionResult(Guid.NewGuid(), Deduplicated: false, DeliveriesEnqueued: request.EnqueueEmail ? 1 : 0));
        }

        public async Task<IReadOnlyList<NotificationIngestionResult>> IngestBatchAsync(
            IReadOnlyCollection<NotificationIngestionRequest> requests,
            CancellationToken cancellationToken = default)
        {
            var results = new List<NotificationIngestionResult>(requests.Count);
            foreach (var request in requests)
            {
                results.Add(await IngestAsync(request, cancellationToken));
            }

            return results;
        }
    }
}

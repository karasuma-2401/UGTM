using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.AddCouncilMembers;
using UGTM.Modules.DefenseCouncil.Application.Councils.ApproveCouncil;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Councils.RemoveCouncilMember;
using UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Architecture.Tests;

public sealed class NotificationPhase4Group5Tests
{
    private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TopicId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GroupId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AdvisorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ReviewerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ChairmanId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SecretaryId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid StudentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid FacultyId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid AaActorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTime NowUtc = new(2026, 11, 26, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAndCancelReviewerAssignment_enqueue_critical_REVIEWER_ASSIGNED_and_ASSIGNMENT_CANCELLED()
    {
        var outbox = new CapturingDefenseNotificationOutbox();
        var auditOutbox = new FakeDefenseAuditOutbox();
        var actorUser = new FakeUser(FacultyId, ["Faculty"], [PermissionCodes.ThesisReviewGatesReview, PermissionCodes.ThesisReviewerAssignmentsManage]);
        var assignmentRepo = new FakeReviewerAssignmentRepository();
        var receiptRepo = new FakeReviewerAssignmentIdempotencyRepository();
        var topicReader = new FakeTopicReader(TopicId, BatchId, AdvisorId, "Hệ thống cảnh báo giao thông thông minh");
        var names = new FakeAdvisorNameReader(ReviewerId, "TS. Trần Văn B");

        var createHandler = new CreateReviewerAssignmentHandler(
            new FakeReviewerWorkflowContract(GroupId, TopicId, BatchId, AdvisorId),
            new FakeReviewerCoordinator(assignmentRepo),
            assignmentRepo,
            receiptRepo,
            topicReader,
            names,
            new ReviewerAssignmentAudit(auditOutbox, actorUser),
            actorUser,
            new FixedTimeProvider(NowUtc),
            outbox);

        var created = await createHandler.HandleAsync(new CreateReviewerAssignmentCommand(
            GroupId,
            ReviewerId,
            Guid.NewGuid(),
            1,
            Guid.NewGuid()));

        Assert.True(created.IsSuccess);
        var assignedEntry = Assert.Single(outbox.Entries);
        Assert.Equal("REVIEWER_ASSIGNED", assignedEntry.NotificationType);
        Assert.Equal("Critical", assignedEntry.Priority);
        Assert.True(assignedEntry.EnqueueEmail);
        Assert.Equal([ReviewerId], assignedEntry.RecipientUserIds);
        Assert.Equal("/thesis/lecturer-grading", assignedEntry.ActionPath);

        outbox.Entries.Clear();
        var cancelHandler = new CancelReviewerAssignmentHandler(
            new FakeReviewerCoordinator(assignmentRepo),
            assignmentRepo,
            receiptRepo,
            new FakeThesisReader(GroupId, TopicId, BatchId),
            topicReader,
            names,
            new ReviewerAssignmentAudit(auditOutbox, actorUser),
            actorUser,
            new FixedTimeProvider(NowUtc.AddMinutes(5)),
            outbox);

        var cancelled = await cancelHandler.HandleAsync(new CancelReviewerAssignmentCommand(
            created.Value.Id,
            assignmentRepo.Stored!.Version,
            "Thay đổi GVPB theo lịch công tác.",
            Guid.NewGuid()));

        Assert.True(cancelled.IsSuccess);
        var cancelledEntry = Assert.Single(outbox.Entries);
        Assert.Equal("ASSIGNMENT_CANCELLED", cancelledEntry.NotificationType);
        Assert.Equal("Critical", cancelledEntry.Priority);
        Assert.True(cancelledEntry.EnqueueEmail);
        Assert.Equal([ReviewerId], cancelledEntry.RecipientUserIds);
        Assert.Contains(cancelledEntry.EmailMetadata!, m => m.Label == "Lý do hủy" && m.Value.Contains("lịch công tác"));
    }

    [Fact]
    public async Task Council_member_schedule_and_approval_handlers_enqueue_expected_SMTP_and_in_app_notifications()
    {
        var council = Council.Create(
            BatchId,
            "HD-01",
            new DateOnly(2026, 12, 20),
            "08:00 - 11:30",
            "B4.02");
        council.Assignments.Add(new CouncilAssignment
        {
            Id = Guid.NewGuid(),
            CouncilId = council.Id,
            GroupId = GroupId,
            OrderNumber = 1
        });

        var councilRepo = new FakeCouncilRepository(council, new HashSet<Guid> { GroupId });
        var userInfoReader = new FakeCouncilUserInfoReader(new Dictionary<Guid, CouncilUserInfoContractDto>
        {
            [ChairmanId] = new(ChairmanId, "PGS.TS Nguyễn Văn X", "x@uit.edu.vn", "GV01", true, true),
            [SecretaryId] = new(SecretaryId, "ThS. Lê Thị Y", "y@uit.edu.vn", "GV02", true, true)
        });
        var groupContract = new FakeDefenseGroupContract(GroupId, TopicId, BatchId, StudentId);
        var topicContract = new FakeTopicDefenseContract(TopicId, BatchId, AdvisorId, "Nền tảng học tập thích ứng");
        var outbox = new CapturingDefenseNotificationOutbox();

        var addMembersHandler = new AddCouncilMembersHandler(
            councilRepo,
            new FakeDefenseUnitOfWork(),
            userInfoReader,
            groupContract,
            topicContract,
            new FakeUser(FacultyId, ["Faculty"], []),
            notificationOutbox: outbox,
            timeProvider: new FixedTimeProvider(NowUtc));

        var added = await addMembersHandler.HandleAsync(new AddCouncilMembersCommand(
            council.Id,
            [
                new CouncilMemberInputDto(ChairmanId, CouncilMemberRole.Chairman),
                new CouncilMemberInputDto(SecretaryId, CouncilMemberRole.Secretary)
            ]));

        Assert.True(added.IsSuccess);
        Assert.Equal(2, outbox.Entries.Count);
        Assert.All(outbox.Entries, e =>
        {
            Assert.Equal("COUNCIL_MEMBER_ASSIGNED", e.NotificationType);
            Assert.Equal("Critical", e.Priority);
            Assert.True(e.EnqueueEmail);
            Assert.Equal("/councils/grading", e.ActionPath);
        });

        outbox.Entries.Clear();
        var updateScheduleHandler = new UpdateCouncilScheduleHandler(
            councilRepo,
            new FakeDefenseUnitOfWork(),
            userInfoReader,
            new FakeUser(FacultyId, ["Faculty"], []),
            notificationOutbox: outbox,
            defenseGroupContract: groupContract,
            timeProvider: new FixedTimeProvider(NowUtc.AddMinutes(5)));

        var rescheduled = await updateScheduleHandler.HandleAsync(new UpdateCouncilScheduleCommand(
            council.Id,
            new DateOnly(2026, 12, 22),
            "13:30 - 17:00",
            "B4.05"));

        Assert.True(rescheduled.IsSuccess);
        Assert.Equal(2, outbox.Entries.Count);
        Assert.All(outbox.Entries, e =>
        {
            Assert.Equal("COUNCIL_SCHEDULE_UPDATED", e.NotificationType);
            Assert.Equal("Critical", e.Priority);
            Assert.True(e.EnqueueEmail);
            Assert.Contains(e.EmailMetadata!, m => m.Label == "Lịch mới" && m.Value.Contains("B4.05"));
        });

        outbox.Entries.Clear();
        council.SubmitToAcademicAffairs(FacultyId, "Đủ điều kiện trình duyệt", NowUtc.AddMinutes(6));
        var approveHandler = new ApproveCouncilHandler(
            councilRepo,
            new FakeDefenseUnitOfWork(),
            groupContract,
            topicContract,
            userInfoReader,
            new FakeDefenseAuditOutbox(),
            new FakeUser(AaActorId, ["AcademicAffairs"], []),
            new PassThroughSchedulingGuard(),
            notificationOutbox: outbox);

        var approved = await approveHandler.HandleAsync(new ApproveCouncilCommand(
            council.Id,
            "123/QĐ-CNPM",
            new DateOnly(2026, 11, 26)));

        Assert.True(approved.IsSuccess);
        Assert.Equal(3, outbox.Entries.Count);
        Assert.Contains(outbox.Entries, e => e.NotificationType == "COUNCIL_MEMBER_ASSIGNED" && e.EnqueueEmail && e.ActionPath == "/councils/grading");
        Assert.Contains(outbox.Entries, e => e.NotificationType == "COUNCIL_APPROVED" && !e.EnqueueEmail && e.ActionPath == "/dashboard");

        outbox.Entries.Clear();
        var draftCouncil = Council.Create(BatchId, "HD-02", new DateOnly(2026, 12, 25), "08:00", "C101");
        draftCouncil.Members.Add(new CouncilMember
        {
            Id = Guid.NewGuid(),
            CouncilId = draftCouncil.Id,
            UserId = SecretaryId,
            RoleInCouncil = CouncilMemberRole.Secretary
        });
        var removeHandler = new RemoveCouncilMemberHandler(
            new FakeCouncilRepository(draftCouncil, new HashSet<Guid>()),
            new FakeDefenseUnitOfWork(),
            userInfoReader,
            new FakeUser(FacultyId, ["Faculty"], []),
            notificationOutbox: outbox,
            timeProvider: new FixedTimeProvider(NowUtc.AddMinutes(10)));

        var removed = await removeHandler.HandleAsync(new RemoveCouncilMemberCommand(draftCouncil.Id, SecretaryId));
        Assert.True(removed.IsSuccess);
        var removedEntry = Assert.Single(outbox.Entries);
        Assert.Equal("ASSIGNMENT_CANCELLED", removedEntry.NotificationType);
        Assert.Equal("Critical", removedEntry.Priority);
        Assert.True(removedEntry.EnqueueEmail);
        Assert.Equal([SecretaryId], removedEntry.RecipientUserIds);
    }

    private sealed class CapturingDefenseNotificationOutbox : IDefenseCouncilNotificationOutbox
    {
        public List<NotificationOutboxEntry> Entries { get; } = [];
        public void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeDefenseAuditOutbox : IDefenseCouncilAuditOutbox
    {
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) { }
    }

    private sealed class FakeDefenseUnitOfWork : IDefenseCouncilUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeReviewerAssignmentRepository : IReviewerAssignmentRepository
    {
        public ReviewerAssignment? Stored { get; private set; }
        public Task<ReviewerAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored?.Id == id ? Stored : null);
        public Task<ReviewerAssignment?> GetActiveByGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored?.GroupId == groupId && Stored.IsActive ? Stored : null);
        public Task<IReadOnlyList<ReviewerAssignment>> GetActiveByReviewerAndFileAsync(Guid reviewerId, Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignment>>([]);
        public Task<ReviewerAssignment?> LockByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored?.Id == id ? Stored : null);
        public Task<bool> HasEvaluationAsync(Guid assignmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public void Add(ReviewerAssignment assignment) => Stored = assignment;
    }

    private sealed class FakeReviewerAssignmentIdempotencyRepository : IReviewerAssignmentIdempotencyRepository
    {
        private readonly List<ReviewerAssignmentIdempotencyRecord> _records = [];
        public Task LockRequestAsync(Guid actorId, string operation, Guid key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ReviewerAssignmentIdempotencyRecord?> GetAsync(Guid actorId, string operation, Guid key, CancellationToken cancellationToken = default)
            => Task.FromResult(_records.SingleOrDefault(r => r.ActorId == actorId && r.Operation == operation && r.IdempotencyKey == key));
        public void Add(ReviewerAssignmentIdempotencyRecord record) => _records.Add(record);
    }

    private sealed class FakeReviewerCoordinator(FakeReviewerAssignmentRepository repo) : IReviewerAssignmentTransactionCoordinator
    {
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow, CancellationToken cancellationToken = default)
            => (await workflow(cancellationToken)).Value;

        public async Task<T> ExecuteWithPostSaveAsync<T>(
            Func<CancellationToken, Task<ReviewerAssignmentTransactionResult<T>>> workflow,
            Func<T, CancellationToken, Task<T>> postSave,
            CancellationToken cancellationToken = default)
        {
            var outcome = await workflow(cancellationToken);
            if (!outcome.ShouldCommit)
            {
                return outcome.Value;
            }

            if (repo.Stored is not null && repo.Stored.Version == 0)
            {
                typeof(ReviewerAssignment).GetProperty(nameof(ReviewerAssignment.Version))!.SetValue(repo.Stored, 1u);
            }

            return await postSave(outcome.Value, cancellationToken);
        }
    }

    private sealed class FakeReviewerWorkflowContract(Guid groupId, Guid topicId, Guid batchId, Guid advisorId) : IReviewerAssignmentWorkflowContract
    {
        public Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid gId, Guid rId, CancellationToken cancellationToken = default)
            => LockForCreationAsync(gId, rId, Guid.NewGuid(), 1, cancellationToken);

        public Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(
            Guid gId, Guid rId, Guid expectedAdvisorEvaluationId, int expectedReportVersionNo, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReviewerAssignmentEligibilityResultContractDto(
                ReviewerAssignmentEligibilityOutcome.Succeeded,
                new ReviewerAssignmentBindingContractDto(
                    groupId, topicId, batchId, advisorId, expectedAdvisorEvaluationId,
                    Guid.NewGuid(), expectedReportVersionNo, Guid.NewGuid(), new string('a', 64), NowUtc.AddDays(-1))));

        public Task<ReviewerAssignmentReportAccessContractDto?> GetActiveReportAccessAsync(Guid reviewerId, Guid fileId, CancellationToken cancellationToken = default)
            => Task.FromResult<ReviewerAssignmentReportAccessContractDto?>(null);

        public Task<ReviewerAssignmentReportAccessContractDto?> LockForEvaluationAsync(Guid assignmentId, Guid reviewerId, CancellationToken cancellationToken = default)
            => Task.FromResult<ReviewerAssignmentReportAccessContractDto?>(null);
    }

    private sealed class FakeThesisReader(Guid groupId, Guid topicId, Guid batchId) : IReviewerAssignmentThesisReader
    {
        public Task<IReadOnlyList<ReviewerAssignmentGroupLinkContractDto>> GetGroupLinksAsync(IReadOnlyCollection<Guid>? groupIds, IReadOnlyCollection<Guid>? topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentGroupLinkContractDto>>([]);
        public Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentThesisContractDto>>([]);
        public Task<IReadOnlyList<ReviewerAssignmentThesisContractDto>> GetByTopicIdsAsync(IReadOnlyCollection<Guid> topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewerAssignmentThesisContractDto>>([]);
        public Task<ReviewerAssignmentThesisContractDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == groupId ? new ReviewerAssignmentThesisContractDto(groupId, topicId, batchId, true, null, false, 1, null, null, null, [StudentId]) : null);
        public Task<ReviewerAssignmentThesisContractDto?> LockAsync(Guid id, CancellationToken cancellationToken = default)
            => GetAsync(id, cancellationToken);
    }

    private sealed class FakeTopicReader(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : IReviewerAssignmentTopicReader
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

    private sealed class FakeAdvisorNameReader(Guid userId, string fullName) : IAdvisorNameReader
    {
        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> advisorIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string> { [userId] = fullName });
    }

    private sealed class FakeCouncilRepository(Council council, IReadOnlySet<Guid> eligibleGroupIds) : ICouncilRepository
    {
        public Task<Council?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(council.Id == id ? council : null);
        public Task<Council?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(council.Id == id ? council : null);
        public Task<IReadOnlyList<Council>> GetCouncilsForOverviewAsync(Guid? batchId = null, CouncilStatus? status = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Council>>([council]);
        public Task<bool> ExistsByCodeInBatchAsync(Guid batchId, string councilCode, Guid? excludeCouncilId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasRoomScheduleConflictAsync(DateOnly defenseDate, string timeSlot, string room, Guid? excludeCouncilId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Guid>> GetConflictingMemberUserIdsAsync(DateOnly defenseDate, string timeSlot, IEnumerable<Guid> userIds, Guid? excludeCouncilId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyDictionary<Guid, Guid>> GetAssignedGroupCouncilsAsync(IEnumerable<Guid> groupIds, Guid? excludeCouncilId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid>());
        public Task<IReadOnlySet<Guid>> GetRejectedDefenseGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        public Task<IReadOnlySet<Guid>> GetDefenseEligibleGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default) => Task.FromResult(eligibleGroupIds);
        public Task<IReadOnlySet<Guid>> LockDefenseEligibleGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default) => Task.FromResult(eligibleGroupIds);
        public Task<Council?> GetCouncilByGroupIdAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<Council?>(council);
        public Task<IReadOnlyDictionary<Guid, Guid>> GetActiveReviewerIdsByGroupIdsAsync(IEnumerable<Guid> groupIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid>());
        public Task AddAsync(Council c, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddMembersAsync(IEnumerable<CouncilMember> members, CancellationToken ct = default)
        {
            foreach (var member in members)
            {
                council.Members.Add(member);
            }
            return Task.CompletedTask;
        }
        public Task RemoveMemberAsync(CouncilMember member, CancellationToken ct = default)
        {
            council.Members.Remove(member);
            return Task.CompletedTask;
        }
        public Task ReplaceMembersAsync(IEnumerable<CouncilMember> existingMembers, IEnumerable<CouncilMember> newMembers, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlySet<Guid>> GetUserIdsWithAnyCouncilMembershipAsync(IEnumerable<Guid> userIds, Guid? excludeCouncilId = null, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        public Task AddAssignmentsAsync(IEnumerable<CouncilAssignment> assignments, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReorderAssignmentsAsync(IReadOnlyList<CouncilAssignment> assignments, IReadOnlyDictionary<Guid, int> orders, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAssignmentAsync(CouncilAssignment assignment, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CouncilAssignment?> GetAssignmentWithCouncilAndMembersAsync(Guid assignmentId, CancellationToken ct = default) => Task.FromResult<CouncilAssignment?>(null);
        public Task<IReadOnlyList<GradingCriteria>> GetGradingCriteriaByBatchIdAsync(Guid batchId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GradingCriteria>>([]);
        public Task<IReadOnlyList<CouncilScore>> GetCouncilScoresWithDetailsByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CouncilScore>>([]);
        public Task<IReadOnlyList<EvaluationDetail>> GetEvaluationDetailsByEvaluationIdAsync(Guid evaluationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EvaluationDetail>>([]);
        public Task<CouncilScore?> GetCouncilScoreAsync(Guid assignmentId, Guid councilMemberId, CancellationToken ct = default) => Task.FromResult<CouncilScore?>(null);
        public Task SaveCouncilScoreAsync(CouncilScore score, IEnumerable<EvaluationDetail> details, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ReviewerAssignment?> GetActiveReviewerAssignmentByGroupIdAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ReviewerAssignment?>(null);
        public Task<AdvisorEvaluation?> GetAdvisorEvaluationByGroupIdAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<AdvisorEvaluation?>(null);
        public Task<ScoreSummary?> GetScoreSummaryByGroupIdAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult<ScoreSummary?>(null);
        public Task UpdateMinuteLockStatusAsync(Guid assignmentId, CouncilMinuteLockStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CouncilMinute?> GetMinuteByAssignmentIdAsync(Guid assignmentId, CancellationToken ct = default) => Task.FromResult<CouncilMinute?>(null);
        public Task SaveMinuteAsync(CouncilMinute minute, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveCouncilMinuteDraftAsync(Guid assignmentId, string? studentFeedback, string? overrideMarker, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCouncilUserInfoReader(IReadOnlyDictionary<Guid, CouncilUserInfoContractDto> users) : ICouncilUserInfoReader
    {
        public Task<CouncilUserInfoContractDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(users.TryGetValue(userId, out var u) ? u : null);
        public Task<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, CouncilUserInfoContractDto>>(
                userIds.Where(users.ContainsKey).Distinct().ToDictionary(id => id, id => users[id]));
    }

    private sealed class FakeDefenseGroupContract(Guid groupId, Guid topicId, Guid batchId, Guid studentId) : IDefenseGroupContract
    {
        private readonly DefenseGroupSnapshotContractDto _dto = new(
            groupId, topicId, batchId, "InProgress", NowUtc.AddDays(-10),
            [new DefenseGroupMemberContractDto(studentId, batchId, "Confirmed")]);

        public Task<DefenseGroupSnapshotContractDto?> GetGroupByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == groupId ? _dto : null);
        public Task<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>> GetGroupsByIdsAsync(IEnumerable<Guid> groupIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, DefenseGroupSnapshotContractDto>>(
                new Dictionary<Guid, DefenseGroupSnapshotContractDto> { [groupId] = _dto });
        public Task<DefenseGroupSnapshotContractDto?> GetActiveGroupByStudentIdAsync(Guid sId, CancellationToken cancellationToken = default)
            => Task.FromResult(sId == studentId ? _dto : null);
    }

    private sealed class FakeTopicDefenseContract(Guid topicId, Guid batchId, Guid advisorId, string titleVi) : ITopicDefenseContract
    {
        private readonly TopicDefenseDetailsContractDto _dto = new(topicId, batchId, advisorId, titleVi, null);
        public Task<TopicDefenseDetailsContractDto?> GetTopicDetailsAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == topicId ? _dto : null);
        public Task<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>> GetTopicsDetailsAsync(IEnumerable<Guid> topicIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TopicDefenseDetailsContractDto>>(
                new Dictionary<Guid, TopicDefenseDetailsContractDto> { [topicId] = _dto });
    }

    private sealed class PassThroughSchedulingGuard : ICouncilSchedulingGuard
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
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

using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.GetProgressMilestones;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ProgressMilestoneApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdvisorId = Guid.NewGuid();

    [Fact]
    public async Task Assigned_advisor_can_create_and_list_milestones_after_gate_one_approval()
    {
        var group = CreateApprovedGroup();
        var fixtures = new WorkflowFixtures(group, AdvisorId);
        var createHandler = fixtures.CreateCreateHandler(AdvisorUser());

        var result = await createHandler.HandleAsync(new CreateProgressMilestoneCommand(
            group.Id,
            "Hoàn thiện chương 1",
            "Nộp bản nháp và tài liệu tham khảo.",
            Now.AddDays(7)));
        var list = await fixtures.CreateGetHandler(AdvisorUser())
            .HandleAsync(new GetProgressMilestonesQuery(group.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.GroupId);
        Assert.Equal("Hoàn thiện chương 1", result.Value.Title);
        Assert.True(list.IsSuccess);
        Assert.Single(list.Value);
        Assert.Equal(result.Value.Id, list.Value[0].Id);
        Assert.Single(fixtures.Audit.Entries);
        Assert.Equal(IntegrationAuditActions.ProgressMilestoneCreated, fixtures.Audit.Entries[0].Action);
        Assert.Equal(1, fixtures.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Milestone_cannot_be_created_before_gate_one_is_approved()
    {
        var group = CreateRegisteredGroup();
        var fixtures = new WorkflowFixtures(group, AdvisorId);

        var result = await fixtures.CreateCreateHandler(AdvisorUser())
            .HandleAsync(new CreateProgressMilestoneCommand(
                group.Id,
                "Khảo sát yêu cầu",
                null,
                Now.AddDays(7)));

        Assert.True(result.IsFailure);
        Assert.Equal(ProgressMilestoneErrors.GateOneNotApproved.Code, result.Error.Code);
        Assert.Empty(fixtures.Milestones.Items);
        Assert.Empty(fixtures.Audit.Entries);
        Assert.Equal(0, fixtures.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Other_advisor_cannot_create_or_view_group_milestones()
    {
        var group = CreateApprovedGroup();
        var fixtures = new WorkflowFixtures(group, AdvisorId);
        var otherAdvisor = new FakeCurrentUser(Guid.NewGuid(), roles: ["Advisor"]);

        var create = await fixtures.CreateCreateHandler(otherAdvisor)
            .HandleAsync(new CreateProgressMilestoneCommand(
                group.Id,
                "Khảo sát yêu cầu",
                null,
                Now.AddDays(7)));
        var list = await fixtures.CreateGetHandler(otherAdvisor)
            .HandleAsync(new GetProgressMilestonesQuery(group.Id));

        Assert.True(create.IsFailure);
        Assert.Equal(ProgressMilestoneErrors.AccessDenied.Code, create.Error.Code);
        Assert.True(list.IsFailure);
        Assert.Equal(ProgressMilestoneErrors.AccessDenied.Code, list.Error.Code);
        Assert.Empty(fixtures.Milestones.Items);
    }

    [Fact]
    public async Task Administrator_can_create_milestone_without_a_topic_advisor_assignment()
    {
        var group = CreateApprovedGroup();
        var fixtures = new WorkflowFixtures(group, assignedAdvisorId: null);
        var administrator = new FakeCurrentUser(Guid.NewGuid(), roles: ["Admin"]);

        var result = await fixtures.CreateCreateHandler(administrator)
            .HandleAsync(new CreateProgressMilestoneCommand(
                group.Id,
                "Phân tích thiết kế",
                null,
                Now.AddDays(10)));

        Assert.True(result.IsSuccess);
        Assert.Equal(administrator.UserId, result.Value.CreatedBy);
        Assert.Single(fixtures.Milestones.Items);
    }

    [Fact]
    public async Task Deadline_must_be_a_future_utc_instant()
    {
        var group = CreateApprovedGroup();
        var fixtures = new WorkflowFixtures(group, AdvisorId);

        var result = await fixtures.CreateCreateHandler(AdvisorUser())
            .HandleAsync(new CreateProgressMilestoneCommand(
                group.Id,
                "Khảo sát yêu cầu",
                null,
                DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Local)));

        Assert.True(result.IsFailure);
        Assert.Equal(ProgressMilestoneErrors.InvalidRequest.Code, result.Error.Code);
        Assert.Empty(fixtures.Milestones.Items);
    }

    private static StudentGroup CreateApprovedGroup()
    {
        var group = CreateRegisteredGroup();
        Assert.True(group.TrySubmitSyllabus());
        Assert.True(group.TryApproveSyllabus());
        return group;
    }

    private static StudentGroup CreateRegisteredGroup()
        => new(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-7));

    private static ICurrentUser AdvisorUser()
        => new FakeCurrentUser(AdvisorId, roles: ["Advisor"]);

    private sealed class WorkflowFixtures(StudentGroup group, Guid? assignedAdvisorId)
    {
        private readonly FakeStudentGroupRepository _groups = new(group);
        private readonly FakeAdvisorAssignmentContract _advisorAssignments = new(group.TopicId, assignedAdvisorId);

        public FakeProgressMilestoneRepository Milestones { get; } = new();
        public FakeAuditOutbox Audit { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();

        public CreateProgressMilestoneHandler CreateCreateHandler(ICurrentUser currentUser)
            => new(
                _groups,
                Milestones,
                _advisorAssignments,
                Audit,
                UnitOfWork,
                currentUser,
                new FixedTimeProvider(Now));

        public GetProgressMilestonesHandler CreateGetHandler(ICurrentUser currentUser)
            => new(_groups, Milestones, _advisorAssignments, currentUser);
    }

    private sealed class FakeCurrentUser(
        Guid userId,
        IEnumerable<string>? roles = null) : ICurrentUser
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

    public sealed class FakeProgressMilestoneRepository : IProgressMilestoneRepository
    {
        public List<ProgressMilestone> Items { get; } = [];

        public Task<ProgressMilestone?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(
            Guid groupId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressMilestone>>(
                Items.Where(item => item.GroupId == groupId)
                    .OrderBy(item => item.Deadline)
                    .ThenBy(item => item.CreatedAt)
                    .ThenBy(item => item.Id)
                    .ToArray());

        public void Add(ProgressMilestone milestone) => Items.Add(milestone);
    }

    private sealed class FakeAdvisorAssignmentContract(Guid topicId, Guid? advisorId)
        : ITopicAdvisorAssignmentContract
    {
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(
            Guid requestedTopicId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<TopicAdvisorAssignmentContractDto?>(
                requestedTopicId == topicId && advisorId.HasValue
                    ? new TopicAdvisorAssignmentContractDto(topicId, advisorId.Value)
                    : null);
    }

    public sealed class FakeAuditOutbox : IThesisManagementAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];

        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    public sealed class FakeUnitOfWork : IThesisUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.GetReviewGateConfigurations;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.UpsertReviewGateConfiguration;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ReviewGateConfigurationApplicationTests
{
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Upsert_creates_a_gate_configuration_and_writes_audit_entry()
    {
        var configurations = new FakeConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateUpsertHandler(configurations, audit, unitOfWork, AuthorizedUser());

        var result = await handler.HandleAsync(ValidCommand(ReviewGateType.Syllabus));

        Assert.True(result.IsSuccess);
        Assert.Equal(BatchId, result.Value.BatchId);
        Assert.Equal(nameof(ReviewGateType.Syllabus), result.Value.GateType);
        Assert.Equal(Now.AddDays(1), result.Value.InitialSubmissionDeadlineUtc);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(audit.Entries);
        Assert.Equal(IntegrationAuditActions.ReviewGateConfigurationCreated, audit.Entries[0].Action);
    }

    [Fact]
    public async Task Upsert_updates_existing_configuration_without_creating_a_second_record()
    {
        var existing = ExistingConfiguration(ReviewGateType.Midterm);
        var configurations = new FakeConfigurationRepository(existing);
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateUpsertHandler(configurations, audit, unitOfWork, AuthorizedUser());
        var command = new UpsertReviewGateConfigurationCommand(
            BatchId,
            ReviewGateType.Midterm,
            Now.AddDays(4),
            Now.AddDays(5),
            Now.AddDays(6));

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.Id);
        Assert.Equal(Now.AddDays(4), existing.InitialSubmissionDeadlineUtc);
        Assert.Equal(0, configurations.AddCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(audit.Entries);
        Assert.Equal(IntegrationAuditActions.ReviewGateConfigurationUpdated, audit.Entries[0].Action);
    }

    [Fact]
    public async Task Upsert_denies_user_without_the_deadline_configuration_permission()
    {
        var configurations = new FakeConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateUpsertHandler(
            configurations,
            audit,
            unitOfWork,
            new FakeCurrentUser(ActorId, roles: ["Faculty"]));

        var result = await handler.HandleAsync(ValidCommand(ReviewGateType.Syllabus));

        Assert.True(result.IsFailure);
        Assert.Equal(ReviewGateConfigurationErrors.AccessDenied.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(0, configurations.AddCount);
    }

    [Fact]
    public async Task Upsert_rejects_deadlines_that_are_not_strictly_increasing()
    {
        var configurations = new FakeConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateUpsertHandler(configurations, audit, unitOfWork, AuthorizedUser());
        var command = new UpsertReviewGateConfigurationCommand(
            BatchId,
            ReviewGateType.Syllabus,
            Now.AddDays(2),
            Now.AddDays(2),
            Now.AddDays(3));

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsFailure);
        Assert.Equal(ReviewGateConfigurationErrors.InvalidRequest.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Equal(0, configurations.AddCount);
    }

    [Fact]
    public async Task Upsert_rejects_configuration_for_an_unknown_batch()
    {
        var configurations = new FakeConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpsertReviewGateConfigurationHandler(
            new FakeBatchReader(exists: false),
            configurations,
            audit,
            unitOfWork,
            AuthorizedUser(),
            new FixedTimeProvider(Now));

        var result = await handler.HandleAsync(ValidCommand(ReviewGateType.Syllabus));

        Assert.True(result.IsFailure);
        Assert.Equal(ReviewGateConfigurationErrors.BatchNotFound.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Get_by_batch_returns_configurations_in_gate_order()
    {
        var midterm = ExistingConfiguration(ReviewGateType.Midterm);
        var syllabus = ExistingConfiguration(ReviewGateType.Syllabus);
        var handler = new GetReviewGateConfigurationsHandler(
            new FakeBatchReader(),
            new FakeConfigurationRepository(midterm, syllabus),
            AuthorizedUser());

        var result = await handler.HandleAsync(new GetReviewGateConfigurationsQuery(BatchId));

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Value,
            item => Assert.Equal(nameof(ReviewGateType.Syllabus), item.GateType),
            item => Assert.Equal(nameof(ReviewGateType.Midterm), item.GateType));
    }

    private static UpsertReviewGateConfigurationCommand ValidCommand(ReviewGateType gateType)
        => new(
            BatchId,
            gateType,
            Now.AddDays(1),
            Now.AddDays(2),
            Now.AddDays(3));

    private static UpsertReviewGateConfigurationHandler CreateUpsertHandler(
        FakeConfigurationRepository configurations,
        FakeAuditOutbox audit,
        FakeUnitOfWork unitOfWork,
        ICurrentUser currentUser)
        => new(
            new FakeBatchReader(),
            configurations,
            audit,
            unitOfWork,
            currentUser,
            new FixedTimeProvider(Now));

    private static ReviewGateConfiguration ExistingConfiguration(ReviewGateType gateType)
        => new(
            Guid.NewGuid(),
            BatchId,
            gateType,
            Now.AddDays(-4),
            Now.AddDays(-3),
            Now.AddDays(-2),
            ActorId,
            Now.AddDays(-5));

    private static ICurrentUser AuthorizedUser()
        => new FakeCurrentUser(
            ActorId,
            permissions: [PermissionCodes.ThesisReviewGateConfigurationsManage]);

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

    private sealed class FakeBatchReader(bool exists = true) : IKltnBatchReader
    {
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(exists && batchId == BatchId);
    }

    private sealed class FakeConfigurationRepository(params ReviewGateConfiguration[] configurations)
        : IReviewGateConfigurationRepository
    {
        private readonly List<ReviewGateConfiguration> _configurations = configurations.ToList();

        public int AddCount { get; private set; }

        public Task<ReviewGateConfiguration?> GetByBatchAndGateAsync(
            Guid batchId,
            ReviewGateType gateType,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_configurations.SingleOrDefault(configuration =>
                configuration.BatchId == batchId && configuration.GateType == gateType));

        public Task<IReadOnlyList<ReviewGateConfiguration>> GetByBatchAsync(
            Guid batchId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ReviewGateConfiguration>>(
                _configurations.Where(configuration => configuration.BatchId == batchId).ToArray());

        public void Add(ReviewGateConfiguration configuration)
        {
            AddCount++;
            _configurations.Add(configuration);
        }
    }

    private sealed class FakeAuditOutbox : IThesisManagementAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];

        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IThesisUnitOfWork
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

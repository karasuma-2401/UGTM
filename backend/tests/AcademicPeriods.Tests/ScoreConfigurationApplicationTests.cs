using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.UpdateDraft;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Tests;

public sealed class ScoreConfigurationApplicationTests
{
    [Fact]
    public async Task Create_first_draft_validates_and_persists_percentage_configuration()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var configurations = new FakeScoreConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(batch, configurations, audit, unitOfWork);

        var result = await handler.HandleAsync(new CreateScoreConfigurationDraftCommand(
            batch.Id,
            "Percentage",
            PercentageComponents(),
            null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.VersionNo);
        Assert.Equal(nameof(ConfigurationVersionStatus.Draft), result.Value.Status);
        Assert.Equal(nameof(ScoreCalculationMethod.Percentage), result.Value.CalculationMethod);
        Assert.Equal(3, result.Value.Components.Count);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(0, unitOfWork.TransactionCount);
        Assert.Single(audit.Entries);
        Assert.Equal(IntegrationAuditActions.ScoreConfigurationDraftCreated, audit.Entries[0].Action);
    }

    [Fact]
    public async Task Create_draft_rejects_invalid_percentage_without_writing()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var configurations = new FakeScoreConfigurationRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var invalid = PercentageComponents();
        invalid[0] = invalid[0] with { Weight = 30m };

        var result = await CreateHandler(batch, configurations, audit, unitOfWork)
            .HandleAsync(new CreateScoreConfigurationDraftCommand(
                batch.Id,
                "Percentage",
                invalid,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreConfigurationErrors.InvalidPercentageTotal.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
        Assert.Null(configurations.Added);
    }

    [Fact]
    public async Task Create_next_draft_clones_active_and_requires_change_reason()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var active = ActiveConfiguration(batch.Id);
        var configurations = new FakeScoreConfigurationRepository(active: active);
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(batch, configurations, audit, unitOfWork);

        var missingReason = await handler.HandleAsync(new CreateScoreConfigurationDraftCommand(
            batch.Id,
            null,
            null,
            null));
        Assert.Equal(ScoreConfigurationErrors.ChangeReasonRequired.Code, missingReason.Error.Code);

        var result = await handler.HandleAsync(new CreateScoreConfigurationDraftCommand(
            batch.Id,
            null,
            null,
            "Điều chỉnh tỷ trọng hội đồng"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.VersionNo);
        Assert.Equal(active.Components.Count, result.Value.Components.Count);
        Assert.Equal("Điều chỉnh tỷ trọng hội đồng", result.Value.ChangeReason);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_draft_reports_invalid_integer_coefficient()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var components = PercentageComponents();
        components[0] = components[0] with { Weight = -1m };
        components[1] = components[1] with { Weight = 2m };
        components[2] = components[2] with { Weight = 1m };

        var result = await CreateHandler(
                batch,
                new FakeScoreConfigurationRepository(),
                new FakeAuditOutbox(),
                new FakeUnitOfWork())
            .HandleAsync(new CreateScoreConfigurationDraftCommand(
                batch.Id,
                "IntegerCoefficient",
                components,
                null));

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreConfigurationErrors.InvalidCoefficient.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_rejects_active_configuration_as_immutable()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var active = ActiveConfiguration(batch.Id);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateScoreConfigurationDraftHandler(
            new FakeScoreConfigurationRepository(active: active, byId: active),
            new FakeAuditOutbox(),
            unitOfWork,
            AuthorizedUser(),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));

        var result = await handler.HandleAsync(new UpdateScoreConfigurationDraftCommand(
            batch.Id,
            active.Id,
            "Percentage",
            PercentageComponents(),
            "Attempt to mutate active"));

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreConfigurationErrors.ActiveNotEditable.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Publish_retires_previous_active_and_activates_draft_with_audit()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var active = ActiveConfiguration(batch.Id);
        var draft = active.CloneAsNextDraft(
            Guid.NewGuid(),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1),
            "Điều chỉnh tỷ trọng hội đồng");
        var configurations = new FakeScoreConfigurationRepository(active: active, draft: draft, byId: draft);
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new PublishScoreConfigurationHandler(
            configurations,
            audit,
            unitOfWork,
            AuthorizedUser(),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));

        var result = await handler.HandleAsync(new PublishScoreConfigurationCommand(batch.Id, draft.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(ConfigurationVersionStatus.Retired, active.Status);
        Assert.Equal(ConfigurationVersionStatus.Active, draft.Status);
        Assert.Equal(2, unitOfWork.SaveCount);
        Assert.Equal(1, unitOfWork.TransactionCount);
        Assert.Collection(
            audit.Entries,
            entry => Assert.Equal(IntegrationAuditActions.ScoreConfigurationRetired, entry.Action),
            entry => Assert.Equal(IntegrationAuditActions.ScoreConfigurationPublished, entry.Action));
    }

    [Fact]
    public async Task Publish_maps_persistence_race_to_concurrency_conflict()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var draft = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            batch.Id,
            1,
            ScoreCalculationMethod.Percentage,
            PercentageComponents().Select(component => new UGTM.Modules.AcademicPeriods.Domain.Grading.ScoreComponentDefinition(
                component.ComponentCode,
                component.ComponentName,
                Enum.Parse<ScoreComponentSource>(component.Source),
                Enum.Parse<ScoreComponentApplicability>(component.Applicability),
                component.Weight,
                component.IsRequired,
                component.MinimumScore,
                component.RequiresApproval,
                component.OrderIndex)),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);
        var unitOfWork = new FakeUnitOfWork
        {
            SaveException = new AcademicPeriodsConcurrencyException(new InvalidOperationException())
        };
        var handler = new PublishScoreConfigurationHandler(
            new FakeScoreConfigurationRepository(draft: draft, byId: draft),
            new FakeAuditOutbox(),
            unitOfWork,
            AuthorizedUser(),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));

        var result = await handler.HandleAsync(new PublishScoreConfigurationCommand(batch.Id, draft.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(ScoreConfigurationErrors.ConcurrencyConflict.Code, result.Error.Code);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    private static CreateScoreConfigurationDraftHandler CreateHandler(
        KltnBatch batch,
        FakeScoreConfigurationRepository configurations,
        FakeAuditOutbox audit,
        FakeUnitOfWork unitOfWork)
        => new(
            new FakeBatchRepository(batch),
            configurations,
            audit,
            unitOfWork,
            AuthorizedUser(),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1)));

    private static ICurrentUser AuthorizedUser()
        => new FakeCurrentUser(
            AcademicPeriodTestData.ActorId,
            [PermissionCodes.AcademicScoreConfigurationsManage, PermissionCodes.AcademicScoreConfigurationsRead]);

    private static ScoreWeightConfig ActiveConfiguration(Guid batchId)
    {
        var configuration = ScoreWeightConfig.CreateDraft(
            Guid.NewGuid(),
            batchId,
            1,
            ScoreCalculationMethod.Percentage,
            PercentageComponents().Select(component => new UGTM.Modules.AcademicPeriods.Domain.Grading.ScoreComponentDefinition(
                component.ComponentCode,
                component.ComponentName,
                Enum.Parse<ScoreComponentSource>(component.Source),
                Enum.Parse<ScoreComponentApplicability>(component.Applicability),
                component.Weight,
                component.IsRequired,
                component.MinimumScore,
                component.RequiresApproval,
                component.OrderIndex)),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);
        configuration.Publish(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc);
        return configuration;
    }

    private static ScoreComponentInput[] PercentageComponents()
        =>
        [
            new("ADVISOR", "Điểm GVHD", "Advisor", "Always", 40m, true, null, false, 1),
            new("REVIEWER", "Điểm GVPB", "Reviewer", "Always", 30m, true, 5m, true, 2),
            new("COUNCIL", "Điểm hội đồng", "CouncilAverage", "Always", 30m, true, null, false, 3)
        ];

    private sealed class FakeCurrentUser(Guid userId, IEnumerable<string> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = permissions.ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FakeBatchRepository(KltnBatch batch) : IKltnBatchRepository
    {
        public Task<KltnBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(batch.Id == id ? batch : null);
        public Task<bool> ExistsAsync(string academicYear, int semester, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public void Add(KltnBatch value) => throw new NotSupportedException();
    }

    private sealed class FakeScoreConfigurationRepository(
        ScoreWeightConfig? active = null,
        ScoreWeightConfig? draft = null,
        ScoreWeightConfig? byId = null) : IScoreConfigurationRepository
    {
        public ScoreWeightConfig? Added { get; private set; }
        public Task<ScoreWeightConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(byId?.Id == id ? byId : null);
        public Task<ScoreWeightConfig?> GetActiveByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(active?.BatchId == batchId ? active : null);
        public Task<ScoreWeightConfig?> GetDraftByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(draft?.BatchId == batchId ? draft : null);
        public Task<int> GetLatestVersionNoAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { active, draft, byId }
                .Where(item => item?.BatchId == batchId)
                .Select(item => item!.VersionNo)
                .DefaultIfEmpty(0)
                .Max());
        public void Add(ScoreWeightConfig configuration) => Added = configuration;
    }

    private sealed class FakeAuditOutbox : IAcademicPeriodsAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeUnitOfWork : IAcademicPeriodsUnitOfWork
    {
        public int SaveCount { get; private set; }
        public int TransactionCount { get; private set; }
        public Exception? SaveException { get; init; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                return Task.FromException<int>(SaveException);
            }

            return Task.FromResult(1);
        }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return operation(cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

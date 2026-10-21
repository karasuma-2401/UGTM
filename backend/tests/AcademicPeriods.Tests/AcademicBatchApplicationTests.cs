using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;
using UGTM.Modules.AcademicPeriods.Application.Batches.CreateKltnBatch;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Batches.Queries;
using UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Tests;

public sealed class AcademicBatchApplicationTests
{
    [Fact]
    public async Task Create_batch_materializes_template_and_commits_audit_atomically()
    {
        var repository = new FakeBatchRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateKltnBatchHandler(
            repository,
            new FakeTemplateRepository(AcademicPeriodTestData.ActiveTemplate()),
            audit,
            unitOfWork,
            AuthorizedUser(PermissionCodes.AcademicBatchesManage),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc));

        var result = await handler.HandleAsync(new CreateKltnBatchCommand(
            "KLTN HK1 2026-2027",
            "2026-2027",
            1,
            AcademicPeriodTestData.BatchStart));

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Added);
        Assert.Equal(7, result.Value.Stages.Count);
        Assert.Equal(AcademicPeriodTestData.BatchEnd, result.Value.EndDate);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(audit.Entries);
        Assert.Equal(IntegrationAuditActions.BatchCreated, audit.Entries[0].Action);
    }

    [Fact]
    public async Task Create_batch_rejects_caller_without_manage_permission_before_writing()
    {
        var repository = new FakeBatchRepository();
        var audit = new FakeAuditOutbox();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateKltnBatchHandler(
            repository,
            new FakeTemplateRepository(AcademicPeriodTestData.ActiveTemplate()),
            audit,
            unitOfWork,
            AuthorizedUser(PermissionCodes.AcademicBatchesRead),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc));

        var result = await handler.HandleAsync(new CreateKltnBatchCommand(
            "KLTN HK1",
            "2026-2027",
            1,
            AcademicPeriodTestData.BatchStart));

        Assert.True(result.IsFailure);
        Assert.Equal(AcademicBatchErrors.AccessDenied.Code, result.Error.Code);
        Assert.Null(repository.Added);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Create_batch_maps_database_race_to_duplicate_period_conflict()
    {
        var unitOfWork = new FakeUnitOfWork
        {
            SaveException = new AcademicPeriodsUniqueConstraintException(
                "ix_kltn_batch_academic_year_semester",
                new InvalidOperationException())
        };
        var handler = new CreateKltnBatchHandler(
            new FakeBatchRepository(),
            new FakeTemplateRepository(AcademicPeriodTestData.ActiveTemplate()),
            new FakeAuditOutbox(),
            unitOfWork,
            AuthorizedUser(PermissionCodes.AcademicBatchesManage),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc));

        var result = await handler.HandleAsync(new CreateKltnBatchCommand(
            "KLTN HK1 2026-2027",
            "2026-2027",
            1,
            AcademicPeriodTestData.BatchStart));

        Assert.True(result.IsFailure);
        Assert.Equal(AcademicBatchErrors.DuplicateAcademicPeriod.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_open_batch_schedule_requires_a_reason_and_does_not_commit_on_failure()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        batch.Open(
            ActiveScoreConfiguration(batch.Id),
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1));
        var unitOfWork = new FakeUnitOfWork();
        var audit = new FakeAuditOutbox();
        var handler = new UpdateKltnBatchScheduleHandler(
            new FakeBatchRepository(batch),
            audit,
            unitOfWork,
            AuthorizedUser(PermissionCodes.AcademicBatchesManage),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(2)));
        var items = batch.Stages.Select(stage => new UpdateStageScheduleItem(
            stage.OrderIndex,
            stage.StageCode,
            stage.StageName,
            stage.StartDate,
            stage.EndDate,
            new DateTimeOffset(stage.DeadlineAtUtc))).ToArray();

        var result = await handler.HandleAsync(new UpdateKltnBatchScheduleCommand(batch.Id, items, null));

        Assert.True(result.IsFailure);
        Assert.Equal(AcademicBatchErrors.InvalidRequest.Code, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Open_batch_requires_an_active_score_configuration()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var unitOfWork = new FakeUnitOfWork();
        var audit = new FakeAuditOutbox();
        var handler = new OpenKltnBatchHandler(
            new FakeBatchRepository(batch),
            new FakeScoreConfigurationRepository(null),
            audit,
            unitOfWork,
            AuthorizedUser(PermissionCodes.AcademicBatchesManage),
            new FixedTimeProvider(AcademicPeriodTestData.OccurredAtUtc.AddMinutes(1)));

        var result = await handler.HandleAsync(new OpenKltnBatchCommand(batch.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(AcademicBatchErrors.ScoreConfigurationRequired.Code, result.Error.Code);
        Assert.Equal(BatchStatus.Draft, batch.Status);
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Current_stage_uses_batch_timezone_and_reports_remaining_deadline_time()
    {
        var batch = AcademicPeriodTestData.DraftBatch();
        var detail = AcademicBatchMapper.ToDetail(batch);
        var now = new DateTime(2026, 9, 7, 5, 0, 0, DateTimeKind.Utc);
        var handler = new GetCurrentStageHandler(
            new FakeQueryReader(detail),
            AuthorizedUser(PermissionCodes.AcademicBatchesRead),
            new FixedTimeProvider(now));

        var result = await handler.HandleAsync(new GetCurrentStageQuery(batch.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(AcademicPeriodTestData.BatchStart, result.Value.CurrentLocalDate);
        Assert.Equal("TOPIC_PREPARATION", result.Value.CurrentStage!.StageCode);
        Assert.False(result.Value.CurrentStage.IsDeadlinePassed);
        Assert.True(result.Value.CurrentStage.RemainingSeconds > 0);
    }

    private static ICurrentUser AuthorizedUser(params string[] permissions)
        => new FakeCurrentUser(AcademicPeriodTestData.ActorId, permissions);

    private static ScoreWeightConfig ActiveScoreConfiguration(Guid batchId)
    {
        var configuration = ScoreWeightConfig.CreateDraft(
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
            AcademicPeriodTestData.ActorId,
            AcademicPeriodTestData.OccurredAtUtc);
        configuration.Publish(AcademicPeriodTestData.ActorId, AcademicPeriodTestData.OccurredAtUtc);
        return configuration;
    }

    private sealed class FakeCurrentUser(Guid userId, IEnumerable<string> permissions) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>();
        public IReadOnlySet<string> Permissions { get; } = permissions.ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FakeBatchRepository(KltnBatch? batch = null) : IKltnBatchRepository
    {
        public KltnBatch? Added { get; private set; }
        public bool Exists { get; init; }
        public Task<KltnBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(batch?.Id == id ? batch : null);
        public Task<bool> ExistsAsync(string academicYear, int semester, CancellationToken cancellationToken = default)
            => Task.FromResult(Exists);
        public void Add(KltnBatch value) => Added = value;
    }

    private sealed class FakeTemplateRepository(ScheduleTemplateVersion? template) : IScheduleTemplateRepository
    {
        public Task<ScheduleTemplateVersion?> GetActiveAsync(string templateCode, CancellationToken cancellationToken = default)
            => Task.FromResult(template);
        public void Add(ScheduleTemplateVersion value) => throw new NotSupportedException();
    }

    private sealed class FakeAuditOutbox : IAcademicPeriodsAuditOutbox
    {
        public List<AuditIntegrationEntry> Entries { get; } = [];
        public void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc) => Entries.Add(entry);
    }

    private sealed class FakeScoreConfigurationRepository(ScoreWeightConfig? active) : IScoreConfigurationRepository
    {
        public Task<ScoreWeightConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(active?.Id == id ? active : null);
        public Task<ScoreWeightConfig?> GetActiveByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(active?.BatchId == batchId ? active : null);
        public Task<ScoreWeightConfig?> GetDraftByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ScoreWeightConfig?>(null);
        public Task<int> GetLatestVersionNoAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(active?.VersionNo ?? 0);
        public void Add(ScoreWeightConfig configuration) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IAcademicPeriodsUnitOfWork
    {
        public int SaveCount { get; private set; }
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
    }

    private sealed class FakeQueryReader(KltnBatchDetailDto? batch) : IAcademicBatchQueryReader
    {
        public Task<PagedResult<KltnBatchSummaryDto>> GetBatchesAsync(
            string? academicYear,
            int? semester,
            string? status,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<KltnBatchSummaryDto>([], 0, pageNumber, pageSize));

        public Task<KltnBatchDetailDto?> GetByIdAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(batch?.Id == batchId ? batch : null);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}

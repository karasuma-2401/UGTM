using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class ScoreConfigurationRepository(AcademicPeriodsDbContext db) : IScoreConfigurationRepository
{
    public Task<ScoreWeightConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.ScoreWeightConfigs
            .Include(x => x.Components)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ScoreWeightConfig?> GetActiveByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        => db.ScoreWeightConfigs
            .Include(x => x.Components)
            .SingleOrDefaultAsync(
                x => x.BatchId == batchId && x.Status == ConfigurationVersionStatus.Active,
                cancellationToken);

    public Task<ScoreWeightConfig?> GetDraftByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
        => db.ScoreWeightConfigs
            .Include(x => x.Components)
            .SingleOrDefaultAsync(
                x => x.BatchId == batchId && x.Status == ConfigurationVersionStatus.Draft,
                cancellationToken);

    public async Task<int> GetLatestVersionNoAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await db.ScoreWeightConfigs
            .Where(x => x.BatchId == batchId)
            .Select(x => (int?)x.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

    public void Add(ScoreWeightConfig configuration) => db.ScoreWeightConfigs.Add(configuration);
}

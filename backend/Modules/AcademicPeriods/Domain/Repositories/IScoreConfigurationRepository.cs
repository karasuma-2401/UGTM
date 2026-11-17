using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Domain.Repositories;

public interface IScoreConfigurationRepository
{
    Task<ScoreWeightConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ScoreWeightConfig?> GetActiveByBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ScoreWeightConfig?> GetDraftByBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionNoAsync(Guid batchId, CancellationToken cancellationToken = default);
    void Add(ScoreWeightConfig configuration);
}

using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Domain.Repositories;

public interface IKltnBatchRepository
{
    Task<KltnBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ReloadAsync(KltnBatch batch, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string academicYear, int semester, CancellationToken cancellationToken = default);
    void Add(KltnBatch batch);
    void AddStages(IEnumerable<Stage> stages);
}

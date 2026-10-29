using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class KltnBatchRepository(AcademicPeriodsDbContext db) : IKltnBatchRepository
{
    public Task<KltnBatch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.KltnBatches
            .Include(x => x.Stages)
            .Include(x => x.ScoreConfigurations)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task ReloadAsync(KltnBatch batch, CancellationToken cancellationToken = default)
        => db.Entry(batch).ReloadAsync(cancellationToken);

    public Task<bool> ExistsAsync(string academicYear, int semester, CancellationToken cancellationToken = default)
    {
        var normalizedYear = academicYear.Trim();
        return db.KltnBatches.AnyAsync(
            x => x.AcademicYear == normalizedYear && x.Semester == semester,
            cancellationToken);
    }

    public void Add(KltnBatch batch) => db.KltnBatches.Add(batch);

    public void AddStages(IEnumerable<Stage> stages) => db.Stages.AddRange(stages);
}

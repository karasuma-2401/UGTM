using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

internal sealed class ScoreSummaryConfigurationReader(AcademicPeriodsDbContext db) : IScoreSummaryConfigurationReader
{
    public Task<ScoreSummaryConfigurationDto?> GetAsync(Guid batchId, CancellationToken token = default) => ReadAsync(batchId, false, token);
    public Task<ScoreSummaryConfigurationDto?> LockAsync(Guid batchId, CancellationToken token = default) => ReadAsync(batchId, true, token);
    private async Task<ScoreSummaryConfigurationDto?> ReadAsync(Guid batch, bool locked, CancellationToken token)
    {
        if (locked && db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared score transaction required.");
        try { return await ReadCoreAsync(batch, locked, token); }
        catch (InvalidOperationException) { return new(Guid.Empty, batch, 0, 0, "Invalid", false, []); }
    }
    private async Task<ScoreSummaryConfigurationDto?> ReadCoreAsync(Guid batch, bool locked, CancellationToken token)
    {
        for (var attempt = 0; attempt < (locked ? 3 : 1); attempt++)
        {
            var candidates = await db.ScoreWeightConfigs.AsNoTracking().Where(c => c.BatchId == batch && c.Status == ConfigurationVersionStatus.Active).ToListAsync(token);
            if (candidates.Count == 0) return null;
            if (candidates.Count != 1) return new(Guid.Empty, batch, 0, 0, "Invalid", false, []);
            var c = candidates[0];
            if (locked)
            {
                c = await db.ScoreWeightConfigs.FromSqlInterpolated($"SELECT c.*,c.xmin FROM academic.score_weight_config c WHERE c.id={c.Id} FOR SHARE").AsNoTracking().SingleAsync(token);
                // READ COMMITTED: a publisher may have retired our candidate while this statement waited.
                if (c.Status != ConfigurationVersionStatus.Active) continue;
                var activeIds = await db.ScoreWeightConfigs.AsNoTracking().Where(v => v.BatchId == batch && v.Status == ConfigurationVersionStatus.Active).Select(v => v.Id).ToListAsync(token);
                if (activeIds.Count != 1 || activeIds[0] != c.Id) continue;
            }
            var components = locked
                ? await db.ScoreComponentConfigs.FromSqlInterpolated($"SELECT d.* FROM academic.score_component_config d WHERE d.config_id={c.Id} ORDER BY d.order_index,d.id FOR SHARE").AsNoTracking().ToListAsync(token)
                : await db.ScoreComponentConfigs.AsNoTracking().Where(d => d.ConfigId == c.Id).OrderBy(d => d.OrderIndex).ToListAsync(token);
            var valid = c.Id != Guid.Empty && c.Version > 0 && c.VersionNo > 0;
            try
            {
                if (!Enum.IsDefined(c.CalculationMethod) || components.Any(d => !Enum.IsDefined(d.Source) || !Enum.IsDefined(d.Applicability))) valid = false;
                _ = ScoreWeightConfig.CreateDraft(Guid.NewGuid(), batch, 1, c.CalculationMethod,
                    components.Select(d => new ScoreComponentDefinition(d.ComponentCode, d.ComponentName, d.Source, d.Applicability, d.Weight, d.IsRequired, d.MinimumScore, d.RequiresApproval, d.OrderIndex)),
                    Guid.NewGuid(), DateTime.UnixEpoch);
            }
            catch (Exception e) when (e is ArgumentException or OverflowException) { valid = false; }
            return new(c.Id, batch, c.VersionNo, c.Version, c.CalculationMethod.ToString(), valid,
                components.OrderBy(d => d.OrderIndex).Select(d => new ScoreSummaryComponentDefinition(d.Id, d.ComponentCode, d.ComponentName, d.Source.ToString(), d.Applicability.ToString(), d.Weight, d.IsRequired, d.MinimumScore, d.RequiresApproval, d.OrderIndex)).ToArray());
        }
        return new(Guid.Empty, batch, 0, 0, "Churn", false, []);
    }
}

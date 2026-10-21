using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class ScoreConfigurationQueryReader(AcademicPeriodsDbContext db)
    : IScoreConfigurationQueryReader
{
    public async Task<ScoreConfigurationDto?> GetByIdAsync(
        Guid batchId,
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await db.ScoreWeightConfigs
            .AsNoTracking()
            .Include(item => item.Components)
            .SingleOrDefaultAsync(
                item => item.BatchId == batchId && item.Id == configurationId,
                cancellationToken);
        return configuration is null ? null : ScoreConfigurationMapper.ToDto(configuration);
    }

    public async Task<ScoreConfigurationDto?> GetActiveAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await db.ScoreWeightConfigs
            .AsNoTracking()
            .Include(item => item.Components)
            .SingleOrDefaultAsync(
                item => item.BatchId == batchId && item.Status == ConfigurationVersionStatus.Active,
                cancellationToken);
        return configuration is null ? null : ScoreConfigurationMapper.ToDto(configuration);
    }

    public async Task<IReadOnlyList<ScoreConfigurationDto>> GetHistoryAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var configurations = await db.ScoreWeightConfigs
            .AsNoTracking()
            .Include(item => item.Components)
            .Where(item => item.BatchId == batchId)
            .OrderByDescending(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        return configurations.Select(ScoreConfigurationMapper.ToDto).ToArray();
    }
}

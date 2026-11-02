namespace UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;

public sealed record ScoreSummaryComponentDefinition(Guid Id, string Code, string Name, string Source, string Applicability,
    decimal Weight, bool IsRequired, decimal? MinimumScore, bool RequiresApproval, int OrderIndex);
public sealed record ScoreSummaryConfigurationDto(Guid Id, Guid BatchId, int VersionNo, uint Version, string Method,
    bool IsValid, IReadOnlyList<ScoreSummaryComponentDefinition> Components);
public interface IScoreSummaryConfigurationReader
{
    Task<ScoreSummaryConfigurationDto?> GetAsync(Guid batchId, CancellationToken token = default);
    Task<ScoreSummaryConfigurationDto?> LockAsync(Guid batchId, CancellationToken token = default);
}

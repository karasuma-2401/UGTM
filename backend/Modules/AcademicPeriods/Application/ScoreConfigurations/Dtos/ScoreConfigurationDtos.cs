using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;

public sealed record ScoreComponentInput(
    string ComponentCode,
    string ComponentName,
    string Source,
    string Applicability,
    decimal Weight,
    bool IsRequired,
    decimal? MinimumScore,
    bool RequiresApproval,
    int OrderIndex);

public sealed record ScoreComponentDto(
    Guid Id,
    string ComponentCode,
    string ComponentName,
    string Source,
    string Applicability,
    decimal Weight,
    bool IsRequired,
    decimal? MinimumScore,
    bool RequiresApproval,
    int OrderIndex);

public sealed record ScoreConfigurationDto(
    Guid Id,
    Guid BatchId,
    int VersionNo,
    string Status,
    string CalculationMethod,
    string? ChangeReason,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? PublishedBy,
    DateTime? PublishedAt,
    DateTime? RetiredAt,
    IReadOnlyList<ScoreComponentDto> Components);

public interface IScoreConfigurationQueryReader
{
    Task<ScoreConfigurationDto?> GetByIdAsync(
        Guid batchId,
        Guid configurationId,
        CancellationToken cancellationToken = default);

    Task<ScoreConfigurationDto?> GetActiveAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScoreConfigurationDto>> GetHistoryAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);
}

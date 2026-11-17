using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.UpdateDraft;

public sealed record UpdateScoreConfigurationDraftCommand(
    Guid BatchId,
    Guid ConfigurationId,
    string CalculationMethod,
    IReadOnlyCollection<ScoreComponentInput> Components,
    string? ChangeReason);

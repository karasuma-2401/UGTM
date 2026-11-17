using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;

public sealed record CreateScoreConfigurationDraftCommand(
    Guid BatchId,
    string? CalculationMethod,
    IReadOnlyCollection<ScoreComponentInput>? Components,
    string? ChangeReason);

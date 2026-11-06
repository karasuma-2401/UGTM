using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

public sealed record SaveAdvisorEvaluationCommand(
    Guid FinalReportId,
    int ExpectedReportVersionNo,
    uint? ExpectedEvaluationVersion,
    decimal? Score,
    string? Comment,
    Guid IdempotencyKey,
    IReadOnlyList<CriterionScoreInputDto>? Details = null);

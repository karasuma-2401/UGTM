namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

public sealed record SaveAdvisorEvaluationCommand(
    Guid FinalReportId,
    int ExpectedReportVersionNo,
    uint? ExpectedEvaluationVersion,
    decimal Score,
    string? Comment,
    Guid IdempotencyKey);

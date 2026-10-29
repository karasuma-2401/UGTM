namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.DecideAdvisorEvaluation;

public sealed record DecideAdvisorEvaluationCommand(
    Guid FinalReportId,
    int ExpectedReportVersionNo,
    uint? ExpectedEvaluationVersion,
    bool? AllowsReviewerAssignment,
    string? RejectionReason,
    Guid IdempotencyKey);

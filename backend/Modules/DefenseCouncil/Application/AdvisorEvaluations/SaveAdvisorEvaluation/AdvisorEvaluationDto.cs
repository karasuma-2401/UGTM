namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

public sealed record AdvisorEvaluationDto(
    Guid Id,
    Guid GroupId,
    Guid FinalReportId,
    int ReportVersionNo,
    Guid SubmissionFileId,
    string SubmissionChecksumSha256,
    decimal Score,
    string Comment,
    Guid EvaluatedBy,
    DateTime EvaluatedAtUtc,
    bool? AllowsReviewerAssignment,
    string? RejectionReason,
    Guid? DecisionBy,
    DateTime? DecisionAtUtc,
    uint EvaluationVersion,
    bool IsLocked);

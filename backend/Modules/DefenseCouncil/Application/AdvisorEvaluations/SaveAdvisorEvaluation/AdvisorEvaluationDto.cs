using UGTM.Modules.DefenseCouncil.Domain.Entities;

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

internal static class AdvisorEvaluationDtoMapper
{
    public static AdvisorEvaluationDto ToDto(AdvisorEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return new AdvisorEvaluationDto(
            evaluation.Id,
            evaluation.GroupId,
            evaluation.FinalReportId!.Value,
            evaluation.ReportVersionNo!.Value,
            evaluation.SubmissionFileId!.Value,
            evaluation.SubmissionChecksumSha256!,
            evaluation.Score,
            evaluation.Comment!,
            evaluation.EvaluatedBy!.Value,
            evaluation.EvaluatedAt,
            evaluation.AllowsReviewerAssignment,
            evaluation.RejectionReason,
            evaluation.DecisionBy,
            evaluation.DecisionAt,
            evaluation.Version,
            evaluation.IsLocked);
    }
}

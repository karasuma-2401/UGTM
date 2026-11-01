using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed record ReviewerEvaluationDto(
    Guid Id, Guid AssignmentId, Guid GroupId, Guid TopicId, Guid BatchId, Guid ReviewerId,
    Guid AdvisorEvaluationId, Guid FinalReportId, int ReportVersionNo, Guid SubmissionFileId,
    string SubmissionChecksumSha256, uint AssignmentVersion, uint EvaluationVersion, string Status,
    bool IsLegacy, decimal? Score, string? Comment, string? DefenseApproval, bool IsLocked,
    bool IsVisibleToStudent, bool IsIgnoredInCalculation, Guid CreatedBy, DateTime CreatedAtUtc,
    Guid UpdatedBy, DateTime UpdatedAtUtc, Guid? SubmittedBy, DateTime? SubmittedAtUtc);

public static class ReviewerEvaluationDtoMapper
{
    public static ReviewerEvaluationDto ToDto(ReviewerEvaluation value, ReviewerAssignment assignment, Guid topicId, Guid batchId)
        => new(value.Id, assignment.Id, assignment.GroupId, topicId, batchId, assignment.ReviewerId,
            assignment.AdvisorEvaluationId!.Value, assignment.FinalReportId!.Value, assignment.ReportVersionNo!.Value,
            assignment.SubmissionFileId!.Value, assignment.SubmissionChecksumSha256!, assignment.Version, value.Version,
            value.Status!.Value.ToString(), value.IsLegacy, value.Score, value.Comment, value.DefenseApproval?.ToString(),
            value.IsLocked, value.IsVisibleToStudent, value.IsIgnoredInCalculation, value.CreatedBy!.Value,
            value.CreatedAtUtc!.Value, value.EvaluatedBy!.Value, value.EvaluatedAt, value.SubmittedBy, value.SubmittedAtUtc);
}

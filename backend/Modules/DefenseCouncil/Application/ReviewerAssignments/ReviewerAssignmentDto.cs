using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public record ReviewerAssignmentDto(
    Guid Id, Guid GroupId, Guid TopicId, Guid BatchId, string TopicTitleVi, string? TopicTitleEn,
    Guid ReviewerId, string? ReviewerFullName, Guid? AdvisorIdAtAssignment, Guid? AdvisorEvaluationId,
    Guid? FinalReportId, int? ReportVersionNo, Guid? SubmissionFileId, string? SubmissionChecksumSha256,
    int AssignmentOrder, bool IsActive, bool IsLegacy, uint AssignmentVersion, Guid AssignedBy,
    DateTime AssignedAtUtc, Guid? CancelledBy, DateTime? CancelledAtUtc, string? CancellationReason);

public static class ReviewerAssignmentDtoMapper
{
    public static ReviewerAssignmentDto ToDto(ReviewerAssignment assignment,
        ReviewerAssignmentTopicContractDto topic, string? reviewerName)
        => new(assignment.Id, assignment.GroupId, topic.TopicId, topic.BatchId, topic.TitleVi, topic.TitleEn,
            assignment.ReviewerId, reviewerName, assignment.AdvisorIdAtAssignment, assignment.AdvisorEvaluationId,
            assignment.FinalReportId, assignment.ReportVersionNo, assignment.SubmissionFileId, assignment.SubmissionChecksumSha256,
            assignment.AssignmentOrder, assignment.IsActive, assignment.IsLegacy, assignment.Version, assignment.AssignedBy,
            assignment.AssignedAt, assignment.CancelledBy, assignment.CancelledAt, assignment.CancellationReason);
}

using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed class ReviewerAssignmentAudit(IDefenseCouncilAuditOutbox outbox, ICurrentUser currentUser)
{
    public void AddCreated(ReviewerAssignment assignment)
    {
        if (!assignment.IsActive || assignment.IsLegacy || currentUser.UserId != assignment.AssignedBy)
            throw new InvalidOperationException("Created audit must describe the authenticated assignment actor.");
        Add(assignment, IntegrationAuditActions.ReviewerAssignmentCreated, assignment.AssignedAt, false);
    }

    public void AddCancelled(ReviewerAssignment assignment)
    {
        if (assignment.IsActive || assignment.CancelledAt is null || currentUser.UserId != assignment.CancelledBy
            || string.IsNullOrWhiteSpace(assignment.CancellationReason))
            throw new InvalidOperationException("Cancelled audit must describe the authenticated cancellation actor.");
        Add(assignment, IntegrationAuditActions.ReviewerAssignmentCancelled, assignment.CancelledAt.Value, true);
    }

    private void Add(ReviewerAssignment value, string action, DateTime atUtc, bool hasCancellationReason)
        => outbox.Add(new AuditIntegrationEntry("ReviewerAssignment", value.Id.ToString(), action, "Succeeded", null,
            new
            {
                AssignmentId = value.Id,
                value.GroupId,
                value.ReviewerId,
                value.AdvisorIdAtAssignment,
                value.AdvisorEvaluationId,
                value.FinalReportId,
                value.ReportVersionNo,
                value.SubmissionFileId,
                value.SubmissionChecksumSha256,
                HasCancellationReason = hasCancellationReason
            }), atUtc);
}

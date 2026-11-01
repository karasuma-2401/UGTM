using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class ReviewerEvaluationAudit(IDefenseCouncilAuditOutbox outbox, ICurrentUser currentUser)
{
    // Call after the first save supplies xmin, before the coordinator commits receipt and audit.
    public void AddSaved(ReviewerEvaluation evaluation, ReviewerAssignment assignment)
    {
        if (!currentUser.IsAuthenticated || evaluation.IsLegacy || assignment.IsLegacy || !assignment.IsActive
            || evaluation.AssignmentId != assignment.Id || evaluation.Version == 0
            || currentUser.UserId != evaluation.EvaluatedBy
            || evaluation.Status is not (ReviewerEvaluationStatus.Draft or ReviewerEvaluationStatus.Submitted))
            throw new InvalidOperationException("The audit must describe the saved workflow row and authenticated actor.");
        var submitted = evaluation.Status == ReviewerEvaluationStatus.Submitted;
        if (submitted && (evaluation.SubmittedBy != evaluation.EvaluatedBy || evaluation.SubmittedAtUtc != evaluation.EvaluatedAt))
            throw new InvalidOperationException("The submission audit must describe the final actor and time.");
        outbox.Add(new AuditIntegrationEntry("ReviewerEvaluation", evaluation.Id.ToString(), submitted
            ? IntegrationAuditActions.ReviewerEvaluationSubmitted : IntegrationAuditActions.ReviewerEvaluationDraftSaved,
            "Succeeded", null, new
            {
                EvaluationId = evaluation.Id,
                evaluation.AssignmentId,
                assignment.GroupId,
                assignment.ReviewerId,
                assignment.AdvisorEvaluationId,
                assignment.FinalReportId,
                assignment.ReportVersionNo,
                assignment.SubmissionFileId,
                assignment.SubmissionChecksumSha256,
                EvaluationVersion = evaluation.Version
            }), evaluation.EvaluatedAt);
    }
}

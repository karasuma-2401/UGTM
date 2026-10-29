using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations;

/// <summary>
/// Read-only boundary used by reviewer assignment. It validates the approved evaluation against
/// the current report binding and group state, rather than exposing a bare approval flag.
/// </summary>
public sealed class ReviewerAssignmentEligibilityContract(
    IAdvisorEvaluationRepository evaluationRepository,
    IAdvisorEvaluationWorkflowContract thesisWorkflow)
    : IReviewerAssignmentEligibilityContract
{
    public async Task<ReviewerAssignmentEligibilityContractDto?> GetReviewerAssignmentEligibilityAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty)
        {
            return null;
        }

        var evaluation = await evaluationRepository.GetByGroupIdAsync(groupId, cancellationToken);
        if (evaluation is null
            || evaluation.IsLegacy
            || !evaluation.IsLocked
            || evaluation.AllowsReviewerAssignment != true
            || evaluation.FinalReportId is null
            || evaluation.ReportVersionNo is null
            || evaluation.SubmissionFileId is null
            || string.IsNullOrWhiteSpace(evaluation.SubmissionChecksumSha256)
            || evaluation.DecisionAt is null)
        {
            return null;
        }

        if (!await thesisWorkflow.IsGroupReadyForReviewerAssignmentAsync(groupId, cancellationToken))
        {
            return null;
        }

        var report = await thesisWorkflow.GetFinalReportContextAsync(evaluation.FinalReportId.Value, cancellationToken);
        if (report is null
            || !report.GroupExists
            || report.GroupId != groupId
            || !report.IsInitialFinal
            || !report.IsUnderEvaluation
            || report.CurrentVersionNo != evaluation.ReportVersionNo.Value
            || report.SubmissionFileId != evaluation.SubmissionFileId
            || !string.Equals(
                report.SubmissionChecksumSha256,
                evaluation.SubmissionChecksumSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ReviewerAssignmentEligibilityContractDto(
            evaluation.Id,
            evaluation.GroupId,
            evaluation.FinalReportId.Value,
            evaluation.ReportVersionNo.Value,
            evaluation.SubmissionFileId.Value,
            evaluation.SubmissionChecksumSha256,
            evaluation.DecisionAt.Value);
    }
}

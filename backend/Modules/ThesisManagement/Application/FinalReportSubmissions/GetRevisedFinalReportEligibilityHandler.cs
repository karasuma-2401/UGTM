using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.ThesisManagement.Application.Common;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;

public sealed record RevisedFinalReportEligibilityDto(bool CanSubmit, string? ReasonCode);

public sealed class GetRevisedFinalReportEligibilityHandler(
    FinalReportSubmissionEligibilityChecker submissionEligibility,
    IReviewerDefenseEligibilityContract reviewerEligibility)
{
    public async Task<Result<RevisedFinalReportEligibilityDto>> HandleAsync(Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var submission = await submissionEligibility.CheckAsync(groupId, cancellationToken,
            revisedAfterReviewer: true);
        if (submission.IsFailure)
        {
            if (submission.Error == FinalReportSubmissionErrors.AuthenticationRequired
                || submission.Error == FinalReportSubmissionErrors.AccessDenied
                || submission.Error == FinalReportSubmissionErrors.GroupNotFound
                || submission.Error == FinalReportSubmissionErrors.InvalidRequest)
                return Result.Failure<RevisedFinalReportEligibilityDto>(submission.Error);
            var reason = submission.Error == FinalReportSubmissionErrors.OutsideAllowedWindow
                ? "OutsideAllowedWindow" : "NotAvailable";
            return Result.Success(new RevisedFinalReportEligibilityDto(false, reason));
        }

        var reviewer = await reviewerEligibility.GetAsync(groupId, cancellationToken);
        return Result.Success(new RevisedFinalReportEligibilityDto(
            reviewer?.CanProceed == true,
            reviewer?.CanProceed == true ? null : "NotAvailable"));
    }
}

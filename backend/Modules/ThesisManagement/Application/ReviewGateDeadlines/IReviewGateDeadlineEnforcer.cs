namespace UGTM.Modules.ThesisManagement.Application.ReviewGateDeadlines;

public interface IReviewGateDeadlineEnforcer
{
    Task<ReviewGateDeadlineEnforcementResult> EnforceAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ReviewGateDeadlineEnforcementResult(int ClosedGroupCount);

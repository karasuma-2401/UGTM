using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public interface IReviewerAssignmentApprovalReader
{
    Task<ReviewerAssignmentEligibilityContractDto?> GetAsync(Guid groupId, bool locked,
        CancellationToken cancellationToken = default);
}

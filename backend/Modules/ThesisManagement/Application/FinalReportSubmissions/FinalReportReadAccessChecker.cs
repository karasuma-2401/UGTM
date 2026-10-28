using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;

public sealed class FinalReportReadAccessChecker(
    ICurrentUser currentUser,
    ITopicAdvisorAssignmentContract advisorAssignmentContract)
{
    public async Task<bool> CanReadAsync(StudentGroup group, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return false;
        }

        bool HasRole(string role) => currentUser.Roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));
        if (ThesisAuthorization.IsAdministrator(currentUser)
            || (HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser))
            || (HasRole("Student") && group.Members.Any(member =>
                member.StudentId == currentUser.UserId.Value
                && member.ConfirmationStatus == MemberConfirmationStatus.Confirmed)))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId.Value;
    }
}

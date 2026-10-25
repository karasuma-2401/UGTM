using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Application.Common;

internal static class ThesisAuthorization
{
    internal static bool IsAdministrator(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && currentUser.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));

    internal static bool CanManageReviewGateConfigurations(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (IsAdministrator(currentUser)
               || currentUser.Permissions.Contains(PermissionCodes.ThesisReviewGateConfigurationsManage));

    internal static bool CanReviewGates(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (IsAdministrator(currentUser)
               || currentUser.Permissions.Contains(PermissionCodes.ThesisReviewGatesReview));

    internal static bool CanReviewRenameRequestAdvisor(ICurrentUser currentUser, Guid advisorId)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (IsAdministrator(currentUser) || currentUser.UserId.Value == advisorId);

    internal static bool CanReviewRenameRequestFaculty(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (IsAdministrator(currentUser)
               || currentUser.Roles.Any(r => string.Equals(r, "Faculty", StringComparison.OrdinalIgnoreCase))
               || currentUser.Permissions.Contains(PermissionCodes.ThesisReviewGatesReview));

    internal static bool CanReviewRenameRequestAcademicAffairs(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (IsAdministrator(currentUser)
               || currentUser.Roles.Any(r => string.Equals(r, "AcademicAffairs", StringComparison.OrdinalIgnoreCase)));
}

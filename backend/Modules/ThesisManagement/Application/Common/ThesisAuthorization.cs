using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.ThesisManagement.Application.Common;

internal static class ThesisAuthorization
{
    internal static bool CanManageReviewGateConfigurations(ICurrentUser currentUser)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (currentUser.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
               || currentUser.Permissions.Contains(PermissionCodes.ThesisReviewGateConfigurationsManage));
}

using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.Common;

internal static class AcademicAuthorization
{
    internal static bool CanReadBatches(ICurrentUser currentUser)
        => HasPermission(currentUser, PermissionCodes.AcademicBatchesRead);

    internal static bool CanManageBatches(ICurrentUser currentUser)
        => HasPermission(currentUser, PermissionCodes.AcademicBatchesManage);

    internal static bool CanReadScoreConfigurations(ICurrentUser currentUser)
        => HasPermission(currentUser, PermissionCodes.AcademicScoreConfigurationsRead);

    internal static bool CanManageScoreConfigurations(ICurrentUser currentUser)
        => HasPermission(currentUser, PermissionCodes.AcademicScoreConfigurationsManage);

    private static bool HasPermission(ICurrentUser currentUser, string permission)
        => currentUser.IsAuthenticated
           && currentUser.UserId.HasValue
           && (currentUser.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
               || currentUser.Permissions.Contains(permission));
}

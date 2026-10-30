using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public static class ReviewerAssignmentAuthorization
{
    public static bool CanRead(IEnumerable<string> roles, IEnumerable<string> permissions)
        => CanManage(roles, permissions) ||
            (roles.Any(role => string.Equals(role, "Faculty", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "AcademicAffairs", StringComparison.OrdinalIgnoreCase))
             && permissions.Contains(PermissionCodes.ThesisReviewerAssignmentsRead, StringComparer.Ordinal));
    public static bool CanManage(IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var roleSet = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return roleSet.Contains("Admin") ||
            ((roleSet.Contains("Faculty") || roleSet.Contains("AcademicAffairs"))
             && permissions.Contains(PermissionCodes.ThesisReviewerAssignmentsManage, StringComparer.Ordinal));
    }
}

using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class ReviewerEvaluationAuthorization(ICurrentUser currentUser, IReviewerCandidateReader candidates)
{
    public static bool IsAdmin(IEnumerable<string> roles) => roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
    public static bool CanWrite(IEnumerable<string> roles)
        => IsAdmin(roles) || roles.Contains("Lecturer", StringComparer.OrdinalIgnoreCase);
    public static bool CanReadManagement(IEnumerable<string> roles, IEnumerable<string> permissions)
        => IsAdmin(roles) || (roles.Any(role => role.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
            || role.Equals("AcademicAffairs", StringComparison.OrdinalIgnoreCase))
            && permissions.Contains(PermissionCodes.ThesisReviewerEvaluationsRead, StringComparer.Ordinal));
    public static bool CanRead(IEnumerable<string> roles, IEnumerable<string> permissions)
        => CanReadManagement(roles, permissions) || roles.Contains("Lecturer", StringComparer.OrdinalIgnoreCase);

    public async Task<bool> CanAccessAsync(ReviewerAssignment assignment, bool writing, CancellationToken token)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty) return false;
        if (IsAdmin(currentUser.Roles) || (!writing && CanReadManagement(currentUser.Roles, currentUser.Permissions))) return true;
        if (!currentUser.Roles.Contains("Lecturer", StringComparer.OrdinalIgnoreCase) || assignment.ReviewerId != actor) return false;
        var account = await candidates.GetAsync(actor, token);
        return account is { IsActive: true, IsLecturer: true };
    }
}

using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Common;

internal static class CouncilMinuteAccess
{
    internal static bool CanView(Council council, ICurrentUser user)
        => user.IsAuthenticated && user.UserId.HasValue
           && (council.Members.Any(member => member.UserId == user.UserId.Value)
               || user.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(role, "AcademicAffairs", StringComparison.OrdinalIgnoreCase)));
}

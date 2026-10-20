using UGTM.Modules.Identity.Application.Users.GetUsers;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public static class UserAccountGroupFilter
{
    public static IQueryable<User> FilterByAccountGroup(this IQueryable<User> users, string? group)
        => group switch
        {
            nameof(UserAccountGroup.Student) => users.Where(user =>
                user.UserRoles.Any(role => role.Role.RoleName == nameof(SystemRoleName.Student))),
            // Include roleless accounts so administrators can still find and repair them.
            nameof(UserAccountGroup.Staff) => users.Where(user =>
                !user.UserRoles.Any(role => role.Role.RoleName == nameof(SystemRoleName.Student))),
            _ => users
        };
}

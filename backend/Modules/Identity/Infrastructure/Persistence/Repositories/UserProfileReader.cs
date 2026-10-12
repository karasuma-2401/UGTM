using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Application.Profiles;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

public sealed class UserProfileReader(IdentityDbContext db) : IUserProfileReader
{
    public async Task<UserProfileDto?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                x.UserCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roleNames = await db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Role.RoleName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissions = await db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.PermissionCode)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var roles = roleNames
            .Select(x => x.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.UserCode,
            roles,
            permissions);
    }
}

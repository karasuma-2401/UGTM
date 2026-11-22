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
                x.UserCode,
                x.PhoneNumber,
                x.Status,
                x.CreatedAt,
                x.ActivatedAt,
                x.LastLoginAt,
                x.PasswordChangedAt,
                x.MustChangePassword
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

        var permissionRows = await db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => new
            {
                x.Permission.PermissionCode,
                x.Permission.PermissionName,
                x.Permission.Module
            })
            .Distinct()
            .OrderBy(x => x.Module)
            .ThenBy(x => x.PermissionCode)
            .ToListAsync(cancellationToken);

        var permissions = permissionRows
            .Select(x => x.PermissionCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var permissionDetails = permissionRows
            .GroupBy(x => x.PermissionCode, StringComparer.Ordinal)
            .Select(g => g.First())
            .Select(x => new UserPermissionDetailDto(x.PermissionCode, x.PermissionName, x.Module))
            .ToList();

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
            permissions,
            user.MustChangePassword,
            user.PhoneNumber,
            user.Status.ToString(),
            user.CreatedAt,
            user.ActivatedAt,
            user.LastLoginAt,
            user.PasswordChangedAt,
            permissionDetails);
    }
}

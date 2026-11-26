using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRecipientDirectory(IdentityDbContext db)
    : INotificationRecipientDirectory
{
    public async Task<IReadOnlyList<NotificationRecipientContractDto>> ResolveActiveRecipientsAsync(
        IReadOnlyCollection<Guid>? userIds = null,
        IReadOnlyCollection<string>? roleNames = null,
        IReadOnlyCollection<string>? permissionCodes = null,
        CancellationToken cancellationToken = default)
    {
        var targetUserIds = userIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        var targetRoleNames = roleNames?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        var targetPermissionCodes = permissionCodes?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (targetUserIds.Length == 0 && targetRoleNames.Length == 0 && targetPermissionCodes.Length == 0)
        {
            return Array.Empty<NotificationRecipientContractDto>();
        }

        var matchedUserIds = new HashSet<Guid>(targetUserIds);

        if (targetRoleNames.Length > 0)
        {
            var roleMatchedIds = await db.UserRoles
                .AsNoTracking()
                .Where(ur => targetRoleNames.Contains(ur.Role.RoleName.ToLower()))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            matchedUserIds.UnionWith(roleMatchedIds);
        }

        if (targetPermissionCodes.Length > 0)
        {
            var permissionMatchedIds = await db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.Role.RolePermissions.Any(
                    rp => targetPermissionCodes.Contains(rp.Permission.PermissionCode.ToLower())))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            matchedUserIds.UnionWith(permissionMatchedIds);
        }

        if (matchedUserIds.Count == 0)
        {
            return Array.Empty<NotificationRecipientContractDto>();
        }

        var idsArray = matchedUserIds.ToArray();
        return await db.Users
            .AsNoTracking()
            .Where(u => idsArray.Contains(u.Id) && u.Status == AccountStatus.Active)
            .OrderBy(u => u.Id)
            .Select(u => new NotificationRecipientContractDto(u.Id, u.FullName, u.Email))
            .ToListAsync(cancellationToken);
    }
}

using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Users;

/// <summary>Hỗ trợ chuẩn hóa, kiểm tra tồn tại và trạng thái ẩn khi gán vai trò cho người dùng.</summary>
internal static class UserRoleResolver
{
    public static async Task<Result<List<string>>> ResolveAsync(
        IUserRepository users,
        IReadOnlyCollection<string> rawRoles,
        IReadOnlySet<string>? existingRoleNames,
        CancellationToken cancellationToken)
    {
        var requestedNames = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleStr in rawRoles)
        {
            if (string.IsNullOrWhiteSpace(roleStr))
            {
                return Result.Failure<List<string>>(IdentityErrors.InvalidRole);
            }

            var trimmed = roleStr.Trim();
            if (long.TryParse(trimmed, out _))
            {
                return Result.Failure<List<string>>(IdentityErrors.InvalidRole);
            }

            if (seen.Add(trimmed))
            {
                requestedNames.Add(trimmed);
            }
        }

        if (requestedNames.Count == 0)
        {
            return Result.Failure<List<string>>(IdentityErrors.InvalidRole);
        }

        var matchedRoles = await users.GetRolesByNamesAsync(requestedNames, cancellationToken);
        if (matchedRoles.Count != requestedNames.Count)
        {
            return Result.Failure<List<string>>(IdentityErrors.InvalidRole);
        }

        var roleByName = matchedRoles.ToDictionary(r => r.RoleName, StringComparer.OrdinalIgnoreCase);
        var resolvedNames = new List<string>(requestedNames.Count);

        foreach (var req in requestedNames)
        {
            if (!roleByName.TryGetValue(req, out var role))
            {
                return Result.Failure<List<string>>(IdentityErrors.InvalidRole);
            }

            var alreadyAssigned = existingRoleNames is not null && existingRoleNames.Contains(role.RoleName);
            if (role.IsHidden && !alreadyAssigned)
            {
                return Result.Failure<List<string>>(IdentityErrors.RoleIsHidden);
            }

            resolvedNames.Add(role.RoleName);
        }

        return Result.Success(resolvedNames);
    }
}

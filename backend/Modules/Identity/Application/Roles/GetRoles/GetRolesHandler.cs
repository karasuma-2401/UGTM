using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.GetRoles;

/// <summary>Xử lý truy vấn danh sách vai trò kèm thống kê số lượng người dùng, số quyền và khả năng ẩn.</summary>
public sealed class GetRolesHandler(IRolePermissionRepository repository)
{
    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> HandleAsync(
        GetRolesQuery query,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetAllRolesWithCountsAsync(query.IncludeHidden, cancellationToken);

        var result = items
            .Select(item =>
            {
                var isSystem = item.Role.IsSystemRole();
                var canHide = !isSystem && item.UserCount == 0 && !item.Role.IsHidden;

                return new RoleSummaryDto(
                    item.Role.Id,
                    item.Role.RoleName,
                    item.Role.Description,
                    isSystem,
                    item.Role.IsHidden,
                    item.UserCount,
                    item.PermissionCount,
                    canHide);
            })
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.RoleId)
            .ToList();

        return Result.Success<IReadOnlyList<RoleSummaryDto>>(result);
    }
}

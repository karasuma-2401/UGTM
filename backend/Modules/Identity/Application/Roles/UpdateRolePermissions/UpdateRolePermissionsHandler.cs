using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Permissions.Dtos;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.UpdateRolePermissions;

public sealed class UpdateRolePermissionsHandler(
    IRolePermissionRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IAuditLogWriter auditLogs)
{
    public Task<Result<RolePermissionsDto>> HandleAsync(
        UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PermissionIds is null)
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidPermission));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var role = await repository.GetRoleWithPermissionsAsync(command.RoleId, ct);
            if (role is null)
            {
                return Result.Failure<RolePermissionsDto>(IdentityErrors.RoleNotFound);
            }

            var targetPermissionIds = command.PermissionIds
                .Distinct()
                .ToList();

            // Bảo vệ vai trò Admin: không cho phép gỡ sạch toàn bộ quyền
            if (role.RoleName == SystemRoleName.Admin && targetPermissionIds.Count == 0)
            {
                return Result.Failure<RolePermissionsDto>(IdentityErrors.CannotRevokeAllAdminPermissions);
            }

            // Kiểm tra tính hợp lệ của tất cả permissionIds
            if (targetPermissionIds.Count > 0)
            {
                var existingPermissions = await repository.GetPermissionsByIdsAsync(targetPermissionIds, ct);
                if (existingPermissions.Count != targetPermissionIds.Count)
                {
                    return Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidPermission);
                }
            }

            var currentPermissionIds = role.RolePermissions
                .Select(rp => rp.PermissionId)
                .ToHashSet();
            var permissionsChanged = !currentPermissionIds.SetEquals(targetPermissionIds);

            if (permissionsChanged)
            {
                var now = clock.UtcNow;
                await repository.UpdateRolePermissionsAsync(role.Id, targetPermissionIds, now, ct);

                // Thu hồi phiên tức thời: xoay SecurityStamp cho tất cả user đang giữ role này
                await repository.RotateSecurityStampForRoleUsersAsync(role.Id, ct);

                var requestedPermissionIds = targetPermissionIds.ToHashSet();
                auditLogs.Add(new AuditEntry(
                    TargetType: "Role",
                    TargetId: role.Id.ToString(),
                    Action: AuditActions.RolePermissionsChanged,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        AddedPermissionIds = requestedPermissionIds
                            .Except(currentPermissionIds)
                            .Order()
                            .ToArray(),
                        RemovedPermissionIds = currentPermissionIds
                            .Except(requestedPermissionIds)
                            .Order()
                            .ToArray(),
                        CurrentPermissionIds = requestedPermissionIds
                            .Order()
                            .ToArray()
                    }));

                await unitOfWork.SaveChangesAsync(ct);

                role = await repository.GetRoleWithPermissionsAsync(role.Id, ct) ?? role;
            }

            var permissions = role.RolePermissions
                .Select(rp => new PermissionDto(
                    rp.Permission.Id,
                    rp.Permission.PermissionCode,
                    rp.Permission.PermissionName,
                    rp.Permission.Module))
                .OrderBy(p => p.Module)
                .ThenBy(p => p.PermissionCode)
                .ToList();

            return Result.Success(new RolePermissionsDto(
                role.Id,
                role.RoleName.ToString(),
                role.Description,
                permissions));
        }, cancellationToken);
    }
}

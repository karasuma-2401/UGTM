using System.Text.RegularExpressions;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Permissions.Dtos;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.CreateRole;

public sealed class CreateRoleHandler(
    IRolePermissionRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IAuditLogWriter auditLogs)
{
    private static readonly Regex RoleNameRegex = new(
        @"^[\p{L}\p{N}_\-\s]+$",
        RegexOptions.Compiled);

    public Task<Result<RolePermissionsDto>> HandleAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RoleName))
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidRoleName));
        }

        var trimmedName = command.RoleName.Trim();
        if (trimmedName.Length is < 2 or > 50)
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidRoleName));
        }

        if (long.TryParse(trimmedName, out _) || !RoleNameRegex.IsMatch(trimmedName) || !trimmedName.Any(char.IsLetter))
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidRoleName));
        }

        if (command.Description is not null && command.Description.Length > 255)
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidInput));
        }

        var targetPermissionIds = command.PermissionIds?
            .Distinct()
            .ToList() ?? [];

        if (targetPermissionIds.Any(id => id <= 0))
        {
            return Task.FromResult(Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidPermission));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // 1. Kiểm tra vai trò đã tồn tại chưa (case-insensitive)
            if (await repository.RoleNameExistsAsync(trimmedName, ct))
            {
                return Result.Failure<RolePermissionsDto>(IdentityErrors.RoleAlreadyExists);
            }

            // 2. Kiểm tra tính hợp lệ của tất cả permissionIds nếu có
            IReadOnlyList<Permission> existingPermissions = [];
            if (targetPermissionIds.Count > 0)
            {
                existingPermissions = await repository.GetPermissionsByIdsAsync(targetPermissionIds, ct);
                if (existingPermissions.Count != targetPermissionIds.Count)
                {
                    return Result.Failure<RolePermissionsDto>(IdentityErrors.InvalidPermission);
                }
            }

            // 3. Khởi tạo Role mới và gán quyền
            var now = clock.UtcNow;
            var created = await repository.CreateRoleAsync(
                trimmedName,
                command.Description,
                targetPermissionIds,
                now,
                ct);

            await unitOfWork.SaveChangesAsync(ct);

            // 4. Ghi Audit Log
            auditLogs.Add(new AuditEntry(
                TargetType: "Role",
                TargetId: created.Id.ToString(),
                Action: AuditActions.RoleCreated,
                Outcome: AuditOutcomes.Success,
                ReasonCode: null,
                Metadata: new
                {
                    RoleName = created.RoleName,
                    Description = created.Description,
                    PermissionIds = targetPermissionIds.Order().ToArray()
                }));

            await unitOfWork.SaveChangesAsync(ct);

            var permissions = existingPermissions
                .Select(p => new PermissionDto(
                    p.Id,
                    p.PermissionCode,
                    p.PermissionName,
                    p.Module))
                .OrderBy(p => p.Module)
                .ThenBy(p => p.PermissionCode)
                .ToList();

            return Result.Success(new RolePermissionsDto(
                created.Id,
                created.RoleName,
                created.Description,
                permissions));
        }, cancellationToken);
    }
}

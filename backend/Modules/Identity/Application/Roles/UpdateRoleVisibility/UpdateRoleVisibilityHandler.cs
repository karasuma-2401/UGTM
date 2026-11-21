using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.UpdateRoleVisibility;

/// <summary>Xử lý ẩn vai trò không sử dụng hoặc khôi phục hiển thị cho vai trò đã ẩn.</summary>
public sealed class UpdateRoleVisibilityHandler(
    IRolePermissionRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs)
{
    public Task<Result<RoleSummaryDto>> HandleAsync(
        UpdateRoleVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        if (command.IsHidden is null)
        {
            return Task.FromResult(Result.Failure<RoleSummaryDto>(IdentityErrors.InvalidInput));
        }

        if (command.RoleId <= 0)
        {
            return Task.FromResult(Result.Failure<RoleSummaryDto>(IdentityErrors.RoleNotFound));
        }

        var hideRequested = command.IsHidden.Value;

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var role = await repository.GetRoleByIdForUpdateAsync(command.RoleId, ct);
            if (role is null)
            {
                return Result.Failure<RoleSummaryDto>(IdentityErrors.RoleNotFound);
            }

            var isSystem = role.IsSystemRole();
            var userCount = await repository.GetRoleUserCountAsync(role.Id, ct);

            if (hideRequested)
            {
                if (isSystem)
                {
                    return Result.Failure<RoleSummaryDto>(IdentityErrors.CannotHideSystemRole);
                }

                if (role.IsHidden)
                {
                    return Result.Failure<RoleSummaryDto>(IdentityErrors.RoleAlreadyHidden);
                }

                if (userCount > 0)
                {
                    return Result.Failure<RoleSummaryDto>(IdentityErrors.RoleInUse);
                }

                role.IsHidden = true;
                auditLogs.Add(new AuditEntry(
                    TargetType: "Role",
                    TargetId: role.Id.ToString(),
                    Action: AuditActions.RoleHidden,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        role.RoleName
                    }));
            }
            else
            {
                if (!role.IsHidden)
                {
                    return Result.Failure<RoleSummaryDto>(IdentityErrors.RoleNotHidden);
                }

                role.IsHidden = false;
                auditLogs.Add(new AuditEntry(
                    TargetType: "Role",
                    TargetId: role.Id.ToString(),
                    Action: AuditActions.RoleUnhidden,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        role.RoleName
                    }));
            }

            await unitOfWork.SaveChangesAsync(ct);

            var permissionCount = await repository.GetRolePermissionCountAsync(role.Id, ct);
            var canHide = !isSystem && userCount == 0 && !role.IsHidden;

            return Result.Success(new RoleSummaryDto(
                role.Id,
                role.RoleName,
                role.Description,
                isSystem,
                role.IsHidden,
                userCount,
                permissionCount,
                canHide));
        }, cancellationToken);
    }
}

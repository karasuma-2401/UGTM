using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Roles.Dtos;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Roles.UpdateRoleDescription;

public sealed class UpdateRoleDescriptionHandler(IRolePermissionRepository repository, IIdentityUnitOfWork unitOfWork, IAuditLogWriter auditLogs)
{
    public Task<Result<RoleSummaryDto>> HandleAsync(int roleId, string? description, CancellationToken cancellationToken)
    {
        if (description is { Length: > 255 })
            return Task.FromResult(Result.Failure<RoleSummaryDto>(IdentityErrors.InvalidInput));
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var role = await repository.GetRoleByIdForUpdateAsync(roleId, ct);
            if (role is null) return Result.Failure<RoleSummaryDto>(IdentityErrors.RoleNotFound);
            var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            if (role.Description != normalized)
            {
                role.Description = normalized;
                auditLogs.Add(new AuditEntry("Role", role.Id.ToString(), AuditActions.RoleDescriptionChanged, AuditOutcomes.Success, null,
                    new { role.RoleName, Description = normalized }));
                await unitOfWork.SaveChangesAsync(ct);
            }
            var userCount = await repository.GetRoleUserCountAsync(role.Id, ct);
            var permissionCount = await repository.GetRolePermissionCountAsync(role.Id, ct);
            var isSystem = role.IsSystemRole();
            return Result.Success(new RoleSummaryDto(role.Id, role.RoleName, role.Description, isSystem, role.IsHidden,
                userCount, permissionCount, !isSystem && !role.IsHidden && userCount == 0));
        }, cancellationToken);
    }
}

using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Users.UpdateUserStatus;

public sealed class UpdateUserStatusHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IAuditLogWriter auditLogs)
{
    public Task<Result<UserSummaryDto>> HandleAsync(
        UpdateUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.Status))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidStatus);
            }

            if (int.TryParse(command.Status.Trim(), out _)
                || !Enum.TryParse<AccountStatus>(command.Status.Trim(), ignoreCase: true, out var newStatus)
                || !Enum.IsDefined(newStatus)
                || newStatus == AccountStatus.PendingActivation)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidStatus);
            }

            var user = await users.GetByIdWithRolesAsync(command.Id, ct);
            if (user is null)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.UserNotFound);
            }

            if (user.Status == AccountStatus.PendingActivation)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidStatusTransition);
            }

            if (newStatus == AccountStatus.Suspended && user.Status == AccountStatus.Disabled)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidStatusTransition);
            }

            if (user.Status != newStatus)
            {
                if (user.Status == AccountStatus.Active && newStatus != AccountStatus.Active)
                {
                    var isAdmin = user.UserRoles.Any(ur => string.Equals(ur.Role.RoleName, nameof(SystemRoleName.Admin), StringComparison.OrdinalIgnoreCase));
                    if (isAdmin)
                    {
                        await users.AcquireAdminMutationLockAsync(ct);
                        var otherActiveAdmins = await users.CountActiveAdminsAsync(user.Id, ct);
                        if (otherActiveAdmins == 0)
                        {
                            return Result.Failure<UserSummaryDto>(IdentityErrors.CannotDisableLastAdmin);
                        }
                    }
                }

                var previousStatus = user.Status;
                var now = clock.UtcNow;
                switch (newStatus)
                {
                    case AccountStatus.Active:
                        user.Reactivate(now);
                        break;
                    case AccountStatus.Suspended:
                        user.Suspend(now);
                        break;
                    case AccountStatus.Disabled:
                        user.Disable(now);
                        break;
                }

                auditLogs.Add(new AuditEntry(
                    TargetType: "User",
                    TargetId: user.Id.ToString("D"),
                    Action: AuditActions.UserStatusChanged,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        PreviousStatus = previousStatus.ToString(),
                        NewStatus = user.Status.ToString()
                    }));

                await unitOfWork.SaveChangesAsync(ct);
            }

            var roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();

            return Result.Success(new UserSummaryDto(
                user.Id,
                user.Email,
                user.FullName,
                user.UserCode,
                user.PhoneNumber,
                user.Status.ToString(),
                roles,
                user.CreatedAt,
                user.LastLoginAt));
        }, cancellationToken);
    }
}

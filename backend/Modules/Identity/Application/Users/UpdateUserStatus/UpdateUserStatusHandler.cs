using UGTM.Modules.Identity.Application.Users.Dtos;

namespace UGTM.Modules.Identity.Application.Users.UpdateUserStatus;

public sealed class UpdateUserStatusHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
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

            if (!Enum.TryParse<AccountStatus>(command.Status.Trim(), ignoreCase: true, out var newStatus)
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

            if (newStatus == AccountStatus.Suspended && user.Status != AccountStatus.Active)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidStatusTransition);
            }

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

            await unitOfWork.SaveChangesAsync(ct);

            var roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList();

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

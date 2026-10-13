using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Users.UpdateUser;

public sealed class UpdateUserHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<Result<UserSummaryDto>> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.FullName))
            {
                return Result.Failure<UserSummaryDto>(
                    Error.Validation("Identity.InvalidInput", "Họ tên không được để trống."));
            }

            var user = await users.GetByIdWithRolesAsync(command.Id, ct);
            if (user is null)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.UserNotFound);
            }

            var userCode = string.IsNullOrWhiteSpace(command.UserCode)
                ? null
                : command.UserCode.Trim().ToUpperInvariant();

            if (userCode != null && userCode != user.UserCode)
            {
                if (await users.ExistsByUserCodeAsync(userCode, user.Id, ct))
                {
                    return Result.Failure<UserSummaryDto>(IdentityErrors.DuplicateAccount);
                }
            }

            var now = clock.UtcNow;
            user.UpdateProfile(command.FullName, command.PhoneNumber, userCode, now);

            List<string> resultRoles;
            if (command.Roles != null)
            {
                var systemRoles = new List<SystemRoleName>();
                foreach (var roleStr in command.Roles)
                {
                    if (!Enum.TryParse<SystemRoleName>(roleStr.Trim(), ignoreCase: true, out var role))
                    {
                        return Result.Failure<UserSummaryDto>(
                            Error.Validation("Identity.InvalidRole", $"Vai trò '{roleStr}' không hợp lệ."));
                    }

                    if (!systemRoles.Contains(role))
                    {
                        systemRoles.Add(role);
                    }
                }

                if (systemRoles.Count == 0)
                {
                    return Result.Failure<UserSummaryDto>(
                        Error.Validation("Identity.InvalidRole", "Người dùng phải có ít nhất một vai trò."));
                }

                await users.UpdateRolesAsync(user, systemRoles, now, ct);
                resultRoles = systemRoles.Select(r => r.ToString()).ToList();
            }
            else
            {
                resultRoles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList();
            }

            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new UserSummaryDto(
                user.Id,
                user.Email,
                user.FullName,
                user.UserCode,
                user.PhoneNumber,
                user.Status.ToString(),
                resultRoles,
                user.CreatedAt,
                user.LastLoginAt));
        }, cancellationToken);
    }
}

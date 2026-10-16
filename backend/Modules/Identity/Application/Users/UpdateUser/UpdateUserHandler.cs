using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Users.Dtos;

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
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidInput);
            }

            var fullName = command.FullName.Trim();
            var userCode = string.IsNullOrWhiteSpace(command.UserCode)
                ? null
                : command.UserCode.Trim().ToUpperInvariant();

            if (fullName.Length > EmailValidator.MaxFullNameLength
                || (userCode is not null && userCode.Length > EmailValidator.MaxUserCodeLength))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidInput);
            }

            if (!PhoneNumberValidator.IsValid(command.PhoneNumber))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidPhoneNumber);
            }

            var user = await users.GetByIdWithRolesAsync(command.Id, ct);
            if (user is null)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.UserNotFound);
            }

            if (userCode != null && userCode != user.UserCode)
            {
                if (await users.ExistsByUserCodeAsync(userCode, user.Id, ct))
                {
                    return Result.Failure<UserSummaryDto>(IdentityErrors.DuplicateAccount);
                }
            }

            var now = clock.UtcNow;

            List<string> resultRoles;
            if (command.Roles != null)
            {
                var systemRoles = new List<SystemRoleName>();
                foreach (var roleStr in command.Roles)
                {
                    if (string.IsNullOrWhiteSpace(roleStr))
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidRole);
                    }

                    var trimmedRole = roleStr.Trim();
                    if (int.TryParse(trimmedRole, out _)
                        || !Enum.TryParse<SystemRoleName>(trimmedRole, ignoreCase: true, out var role)
                        || !Enum.IsDefined(role))
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidRole);
                    }

                    if (!systemRoles.Contains(role))
                    {
                        systemRoles.Add(role);
                    }
                }

                if (systemRoles.Count == 0)
                {
                    return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidRole);
                }

                var currentRoles = user.UserRoles.Select(ur => ur.Role.RoleName).ToHashSet();
                if (systemRoles.Contains(SystemRoleName.Student))
                {
                    if (systemRoles.Count > 1)
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.StudentRoleConflict);
                    }

                    if (currentRoles.Any(role => role != SystemRoleName.Student))
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.CannotBecomeStudent);
                    }
                }

                var rolesChanged = !currentRoles.SetEquals(systemRoles);
                if (rolesChanged)
                {
                    await users.UpdateRolesAsync(user, systemRoles, now, ct);
                    user.RotateSecurityStamp();
                }

                resultRoles = systemRoles.Select(r => r.ToString()).ToList();
            }
            else
            {
                resultRoles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList();
            }

            user.UpdateProfile(fullName, command.PhoneNumber, userCode, now);
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

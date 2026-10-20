using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Users.UpdateUser;

public sealed class UpdateUserHandler(
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IAuditLogWriter auditLogs)
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
            var changedFields = new List<string>();
            var normalizedPhoneNumber = string.IsNullOrWhiteSpace(command.PhoneNumber)
                ? null
                : command.PhoneNumber.Trim();

            if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal))
            {
                changedFields.Add("fullName");
            }

            if (!string.Equals(user.PhoneNumber, normalizedPhoneNumber, StringComparison.Ordinal))
            {
                changedFields.Add("phoneNumber");
            }

            if (!string.Equals(user.UserCode, userCode, StringComparison.Ordinal))
            {
                changedFields.Add("userCode");
            }

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

                var currentRoles = user.UserRoles.Select(ur => ur.Role.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var studentRoleName = nameof(SystemRoleName.Student);
                if (systemRoles.Contains(SystemRoleName.Student))
                {
                    if (systemRoles.Count > 1)
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.StudentRoleConflict);
                    }

                    if (currentRoles.Any(role => !string.Equals(role, studentRoleName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return Result.Failure<UserSummaryDto>(IdentityErrors.CannotBecomeStudent);
                    }
                }

                var targetRoleNames = systemRoles.Select(r => r.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var rolesChanged = !currentRoles.SetEquals(targetRoleNames);
                if (rolesChanged)
                {
                    var adminRoleName = nameof(SystemRoleName.Admin);
                    if (currentRoles.Contains(adminRoleName) && !targetRoleNames.Contains(adminRoleName))
                    {
                        await users.AcquireAdminMutationLockAsync(ct);
                        var otherActiveAdmins = await users.CountActiveAdminsAsync(user.Id, ct);
                        if (otherActiveAdmins == 0)
                        {
                            return Result.Failure<UserSummaryDto>(IdentityErrors.CannotRemoveLastAdminRole);
                        }
                    }

                    await users.UpdateRolesAsync(user, systemRoles, now, ct);
                    user.RotateSecurityStamp();

                    auditLogs.Add(new AuditEntry(
                        TargetType: "User",
                        TargetId: user.Id.ToString("D"),
                        Action: AuditActions.UserRolesChanged,
                        Outcome: AuditOutcomes.Success,
                        ReasonCode: null,
                        Metadata: new
                        {
                            AddedRoles = targetRoleNames
                                .Except(currentRoles)
                                .Order()
                                .ToArray(),
                            RemovedRoles = currentRoles
                                .Except(targetRoleNames)
                                .Order()
                                .ToArray(),
                            CurrentRoles = targetRoleNames
                                .Order()
                                .ToArray()
                        }));
                }

                resultRoles = systemRoles.Select(r => r.ToString()).ToList();
            }
            else
            {
                resultRoles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
            }

            if (changedFields.Count > 0)
            {
                user.UpdateProfile(fullName, normalizedPhoneNumber, userCode, now);
                auditLogs.Add(new AuditEntry(
                    TargetType: "User",
                    TargetId: user.Id.ToString("D"),
                    Action: AuditActions.UserUpdated,
                    Outcome: AuditOutcomes.Success,
                    ReasonCode: null,
                    Metadata: new
                    {
                        ChangedFields = changedFields.Order().ToArray()
                    }));
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

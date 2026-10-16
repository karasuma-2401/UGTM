using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Users.Dtos;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Users.CreateUser;

public sealed class CreateUserHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IIdentityEmailOutbox emailOutbox,
    IOpaqueTokenService tokens,
    IPasswordPolicy passwordPolicy,
    IPasswordService passwordService,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<UserSummaryDto>> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.FullName))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidInput);
            }

            var email = command.Email.Trim();
            var fullName = command.FullName.Trim();
            var userCode = string.IsNullOrWhiteSpace(command.UserCode)
                ? null
                : command.UserCode.Trim().ToUpperInvariant();

            if (!EmailValidator.IsValid(email)
                || fullName.Length > EmailValidator.MaxFullNameLength
                || (userCode is not null && userCode.Length > EmailValidator.MaxUserCodeLength))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidInput);
            }

            if (!PhoneNumberValidator.IsValid(command.PhoneNumber))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidPhoneNumber);
            }

            if (command.Roles is null || command.Roles.Count == 0)
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.InvalidRole);
            }

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

            var normalizedEmail = email.ToUpperInvariant();

            if (await users.ExistsByEmailAsync(normalizedEmail, excludeUserId: null, ct))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.DuplicateAccount);
            }

            if (userCode != null && await users.ExistsByUserCodeAsync(userCode, excludeUserId: null, ct))
            {
                return Result.Failure<UserSummaryDto>(IdentityErrors.DuplicateAccount);
            }

            var now = clock.UtcNow;
            string? passwordHash = null;

            if (!string.IsNullOrWhiteSpace(command.InitialPassword))
            {
                var validation = passwordPolicy.Validate(command.InitialPassword);
                if (!validation.IsValid)
                {
                    return Result.Failure<UserSummaryDto>(IdentityErrors.WeakPassword);
                }
            }

            var userId = Guid.NewGuid();
            var user = User.Create(
                userId,
                email,
                normalizedEmail,
                fullName,
                userCode,
                command.PhoneNumber,
                passwordHash: null,
                AccountStatus.PendingActivation,
                now);

            if (!string.IsNullOrWhiteSpace(command.InitialPassword))
            {
                passwordHash = passwordService.Hash(user, command.InitialPassword);
                user.Activate(passwordHash, now);
            }
            else if (command.SendActivationEmail)
            {
                var token = tokens.CreateToken();
                var challenge = OneTimeChallenge.Create(
                    user.Id,
                    ChallengePurpose.ActivateAccount,
                    token.Selector,
                    token.SecretHash,
                    user.Email,
                    policy.ChallengeMaxAttempts,
                    now,
                    now.Add(policy.ActivationLifetime));

                challenges.Add(challenge);
                emailOutbox.EnqueueActivationEmail(
                    user.Id,
                    user.Email,
                    user.FullName,
                    token.RawToken,
                    now);
            }

            users.Add(user);
            foreach (var role in systemRoles)
            {
                await users.AddToRoleAsync(user, role, now, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new UserSummaryDto(
                user.Id,
                user.Email,
                user.FullName,
                user.UserCode,
                user.PhoneNumber,
                user.Status.ToString(),
                systemRoles.Select(r => r.ToString()).ToList(),
                user.CreatedAt,
                user.LastLoginAt));
        }, cancellationToken);
    }
}

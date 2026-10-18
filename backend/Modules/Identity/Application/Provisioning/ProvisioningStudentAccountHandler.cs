using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Provisioning;

public sealed class ProvisioningStudentAccountHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IIdentityEmailOutbox emailOutbox,
    IOpaqueTokenService tokens,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<Guid>> HandleAsync(
        ProvisioningStudentAccountCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var email = command.Email.Trim();
            var normalizedEmail = email.ToUpperInvariant();
            var userCode = command.UserCode.Trim().ToUpperInvariant();

            await users.AcquireAccountProvisioningLockAsync(ct);

            if (await users.ExistsByEmailOrCodeAsync(normalizedEmail, userCode, ct))
            {
                return Result.Failure<Guid>(IdentityErrors.DuplicateAccount);
            }

            var now = clock.UtcNow;
            var user = User.ProvisionStudent(
                Guid.NewGuid(),
                email,
                normalizedEmail,
                command.FullName,
                userCode,
                command.PhoneNumber,
                now);

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

            users.Add(user);
            challenges.Add(challenge);
            await users.AddToRoleAsync(
                user,
                SystemRoleName.Student,
                now,
                ct);

            emailOutbox.EnqueueActivationEmail(
                user.Id,
                user.Email,
                user.FullName,
                token.RawToken,
                now);

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(user.Id);

        }, cancellationToken);
    }
}

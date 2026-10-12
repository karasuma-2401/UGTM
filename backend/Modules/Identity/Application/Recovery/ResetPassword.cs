using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Recovery;

public sealed record ResetPasswordCommand(string ResetGrant, string NewPassword);

public sealed class ResetPasswordHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IAuthenticationSessionRepository sessions,
    IOpaqueTokenService tokens,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<Result> HandleAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (!passwordPolicy.Validate(command.NewPassword).IsValid)
        {
            return Task.FromResult(Result.Failure(IdentityErrors.WeakPassword));
        }

        if (!tokens.TryParse(command.ResetGrant, out var selector, out var validator))
        {
            return Task.FromResult(Result.Failure(IdentityErrors.InvalidResetGrant));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var grant = await challenges.GetForUpdateAsync(
                selector,
                ChallengePurpose.PasswordResetGrant,
                ct);

            if (grant is null
                || !grant.CanAttempt(now)
                || !tokens.Verify(validator, grant.SecretHash))
            {
                return Result.Failure(IdentityErrors.InvalidResetGrant);
            }

            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null || user.Status != AccountStatus.Active)
            {
                return Result.Failure(IdentityErrors.InvalidResetGrant);
            }

            user.ChangePassword(passwords.Hash(user, command.NewPassword), now);
            grant.Consume(now);
            await sessions.RevokeAllAsync(
                user.Id,
                SessionRevocationReason.PasswordReset,
                now,
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}
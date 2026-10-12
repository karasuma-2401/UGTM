using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Application.Persistence;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Activation;

public sealed class ResendActivationHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IOpaqueTokenService tokens,
    IIdentityEmailOutbox emailOutbox,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result> HandleAsync(
        ResendActivationCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var identifier = command.Identifier.Trim().ToUpperInvariant();
            var user = await users.GetByIdentifierAsync(identifier, identifier, ct);
            if (user is null || user.Status != AccountStatus.PendingActivation)
            {
                return Result.Success();
            }

            var now = clock.UtcNow;
            await challenges.InvalidateOutstandingAsync(
                user.Id,
                ChallengePurpose.ActivateAccount,
                now,
                ct);

            var token = tokens.CreateToken();
            challenges.Add(OneTimeChallenge.Create(
                user.Id,
                ChallengePurpose.ActivateAccount,
                token.Selector,
                token.SecretHash,
                user.Email,
                policy.ChallengeMaxAttempts,
                now,
                now.Add(policy.ActivationLifetime)));

            emailOutbox.EnqueueActivationEmail(
                user.Id,
                user.Email,
                user.FullName,
                token.RawToken,
                now);

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}
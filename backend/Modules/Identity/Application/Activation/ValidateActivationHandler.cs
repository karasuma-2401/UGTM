using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Application.Security;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Activation;

public sealed class ValidateActivationHandler(
    IOneTimeChallengeRepository challenges,
    IUserRepository users,
    IOpaqueTokenService tokens,
    IClock clock) 
{
    public async Task<Result<ValidateActivationResult>> HandleAsync(
        ValidateActivationCommand command,
        CancellationToken cancellationToken)
    {
        if (!tokens.TryParse(command.Token, out var selector, out var validator))
        {
            return Result.Failure<ValidateActivationResult>(
                IdentityErrors.InvalidActivationToken);
        }

        var challenge = await challenges.GetAsync(
            selector,
            ChallengePurpose.ActivateAccount,
            cancellationToken);

        if (challenge is null
            || !challenge.CanAttempt(clock.UtcNow)
            || !tokens.Verify(validator, challenge.SecretHash))
        {
            return Result.Failure<ValidateActivationResult>(
                IdentityErrors.InvalidActivationToken);
        }

        var user = await users.GetByIdAsync(challenge.UserId, cancellationToken);
        if (user is null || user.Status != AccountStatus.PendingActivation)
        {
            return Result.Failure<ValidateActivationResult>(
                IdentityErrors.InvalidActivationToken);
        }

        return Result.Success(new ValidateActivationResult(
            MaskEmail(user.Email),
            user.FullName,
            challenge.ExpiresAt));
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return $"***{email[at..]}";
        }

        return $"{email[0]}***{email[(at - 1)..]}";
    }
}
namespace UGTM.Modules.Identity.Application.Activation;

public sealed class CompleteActivationHandler(
    IOneTimeChallengeRepository challenges,
    IUserRepository users,
    IOpaqueTokenService tokens,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public Task<Result> HandleAsync(
        CompleteActivationCommand command,
        CancellationToken cancellationToken)
    {
        var validation = passwordPolicy.Validate(command.Password);
        if (!validation.IsValid)
        {
            return Task.FromResult(Result.Failure(IdentityErrors.WeakPassword));
        }

        if (!tokens.TryParse(command.Token, out var selector, out var validator))
        {
            return Task.FromResult(Result.Failure(
                IdentityErrors.InvalidActivationToken));
        }

        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var challenge = await challenges.GetForUpdateAsync(
                selector,
                ChallengePurpose.ActivateAccount,
                ct);

            if (challenge is null || !challenge.CanAttempt(now))
            {
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            if (!tokens.Verify(validator, challenge.SecretHash))
            {
                challenge.RecordFailedAttempt(now);
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            var user = await users.GetByIdAsync(challenge.UserId, ct);
            if (user is null || user.Status != AccountStatus.PendingActivation)
            {
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            user.Activate(passwords.Hash(user, command.Password), now);
            challenge.Consume(now);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}

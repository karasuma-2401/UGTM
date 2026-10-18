using UGTM.Modules.Identity.Application.AuditLogs;

namespace UGTM.Modules.Identity.Application.Activation;

public sealed class CompleteActivationHandler(
    IOneTimeChallengeRepository challenges,
    IUserRepository users,
    IOpaqueTokenService tokens,
    IPasswordService passwords,
    IPasswordPolicy passwordPolicy,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock)
{
    public Task<Result> HandleAsync(
        CompleteActivationCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!passwordPolicy.Validate(command.Password).IsValid)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.AccountActivated,
                    AuditOutcomes.Denied,
                    IdentityErrors.WeakPassword.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.WeakPassword);
            }

            if (!tokens.TryParse(command.Token, out var selector, out var validator))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.AccountActivated,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidActivationToken.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            var now = clock.UtcNow;
            var challenge = await challenges.GetForUpdateAsync(
                selector,
                ChallengePurpose.ActivateAccount,
                ct);

            if (challenge is null || !challenge.CanAttempt(now))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    challenge?.UserId,
                    AuditActions.AccountActivated,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidActivationToken.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            if (!tokens.Verify(validator, challenge.SecretHash))
            {
                challenge.RecordFailedAttempt(now);
                auditLogs.Add(AuthenticationAudit.ForUser(
                    challenge.UserId,
                    AuditActions.AccountActivated,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidActivationToken.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            var user = await users.GetByIdAsync(challenge.UserId, ct);
            if (user is null || user.Status != AccountStatus.PendingActivation)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    challenge.UserId,
                    AuditActions.AccountActivated,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidActivationToken.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure(IdentityErrors.InvalidActivationToken);
            }

            user.Activate(passwords.Hash(user, command.Password), now);
            challenge.Consume(now);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.AccountActivated,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}

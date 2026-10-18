using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Activation;

public sealed class ResendActivationHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IOpaqueTokenService tokens,
    IIdentityEmailOutbox emailOutbox,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result> HandleAsync(
        ResendActivationCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (string.IsNullOrWhiteSpace(command.Identifier))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.ActivationRequested,
                    AuditOutcomes.Success,
                    metadata: new { NotificationQueued = false }));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success();
            }

            var identifier = command.Identifier.Trim().ToUpperInvariant();
            var user = await users.GetByIdentifierAsync(identifier, identifier, ct);
            if (user is null || user.Status != AccountStatus.PendingActivation)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user?.Id,
                    AuditActions.ActivationRequested,
                    AuditOutcomes.Success,
                    metadata: new { NotificationQueued = false }));
                await unitOfWork.SaveChangesAsync(ct);
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

            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.ActivationRequested,
                AuditOutcomes.Success,
                metadata: new { NotificationQueued = true }));

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success();
        }, cancellationToken);
    }
}

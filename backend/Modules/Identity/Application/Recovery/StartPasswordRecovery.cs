using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Notifications;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Recovery;

public sealed record StartPasswordRecoveryCommand(string Email);
public sealed record StartPasswordRecoveryResult(string ChallengeId, DateTime ExpiresAt);

public sealed class StartPasswordRecoveryHandler(
    IUserRepository users,
    IOneTimeChallengeRepository challenges,
    IOpaqueTokenService tokens,
    IIdentityEmailOutbox emailOutbox,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<StartPasswordRecoveryResult>> HandleAsync(
        StartPasswordRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var expiresAt = now.Add(policy.RecoveryOtpLifetime);
            if (string.IsNullOrWhiteSpace(command.Email))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    null,
                    AuditActions.PasswordRecoveryRequested,
                    AuditOutcomes.Success,
                    metadata: new { NotificationQueued = false }));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success(new StartPasswordRecoveryResult(
                    Guid.NewGuid().ToString("N"),
                    expiresAt));
            }

            var normalizedEmail = command.Email.Trim().ToUpperInvariant();
            var user = await users.GetByNormalizedEmailAsync(normalizedEmail, ct);

            if (user is null || user.Status != AccountStatus.Active)
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    user?.Id,
                    AuditActions.PasswordRecoveryRequested,
                    AuditOutcomes.Success,
                    metadata: new { NotificationQueued = false }));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Success(new StartPasswordRecoveryResult(
                    Guid.NewGuid().ToString("N"),
                    expiresAt));
            }

            await challenges.InvalidateOutstandingAsync(
                user.Id,
                ChallengePurpose.PasswordResetOtp,
                now,
                ct);

            var otp = tokens.CreateNumericCode(6);
            challenges.Add(OneTimeChallenge.Create(
                user.Id,
                ChallengePurpose.PasswordResetOtp,
                otp.Selector,
                otp.SecretHash,
                user.Email,
                policy.ChallengeMaxAttempts,
                now,
                expiresAt));

            emailOutbox.EnqueuePasswordRecoveryEmail(
                user.Id,
                user.Email,
                user.FullName,
                otp.Code,
                now);
            auditLogs.Add(AuthenticationAudit.ForUser(
                user.Id,
                AuditActions.PasswordRecoveryRequested,
                AuditOutcomes.Success,
                metadata: new { NotificationQueued = true }));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new StartPasswordRecoveryResult(
                otp.Selector,
                expiresAt));
        }, cancellationToken);
    }
}

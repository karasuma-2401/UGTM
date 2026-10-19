using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Domain.Entities;

namespace UGTM.Modules.Identity.Application.Recovery;

public sealed record VerifyRecoveryOtpCommand(string ChallengeId, string Code);
public sealed record VerifyRecoveryOtpResult(string ResetGrant, DateTime ExpiresAt);

public sealed class VerifyRecoveryOtpHandler(
    IOneTimeChallengeRepository challenges,
    IOpaqueTokenService tokens,
    IIdentityUnitOfWork unitOfWork,
    IAuditLogWriter auditLogs,
    IClock clock,
    IdentitySecurityPolicy policy)
{
    public Task<Result<VerifyRecoveryOtpResult>> HandleAsync(
        VerifyRecoveryOtpCommand command,
        CancellationToken cancellationToken)
    {
        return unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = clock.UtcNow;
            var otp = await challenges.GetForUpdateAsync(
                command.ChallengeId,
                ChallengePurpose.PasswordResetOtp,
                ct);

            if (otp is null || !otp.CanAttempt(now))
            {
                auditLogs.Add(AuthenticationAudit.ForUser(
                    otp?.UserId,
                    AuditActions.RecoveryOtpVerified,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidOtp.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<VerifyRecoveryOtpResult>(IdentityErrors.InvalidOtp);
            }

            if (!tokens.Verify(command.Code, otp.SecretHash))
            {
                otp.RecordFailedAttempt(now);
                auditLogs.Add(AuthenticationAudit.ForUser(
                    otp.UserId,
                    AuditActions.RecoveryOtpVerified,
                    AuditOutcomes.Denied,
                    IdentityErrors.InvalidOtp.Code));
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Failure<VerifyRecoveryOtpResult>(IdentityErrors.InvalidOtp);
            }

            otp.Consume(now);
            var grantToken = tokens.CreateToken();
            var expiresAt = now.Add(policy.ResetGrantLifetime);
            challenges.Add(OneTimeChallenge.Create(
                otp.UserId,
                ChallengePurpose.PasswordResetGrant,
                grantToken.Selector,
                grantToken.SecretHash,
                otp.DestinationSnapshot,
                1,
                now,
                expiresAt));
            auditLogs.Add(AuthenticationAudit.ForUser(
                otp.UserId,
                AuditActions.RecoveryOtpVerified,
                AuditOutcomes.Success));
            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new VerifyRecoveryOtpResult(
                grantToken.RawToken,
                expiresAt));
        }, cancellationToken);
    }
}

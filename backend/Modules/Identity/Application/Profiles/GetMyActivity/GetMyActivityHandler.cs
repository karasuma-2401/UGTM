using System.Globalization;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.AuditLogs.Dtos;
using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Application.Profiles.GetMyActivity;

public sealed class GetMyActivityHandler(
    ICurrentUser currentUser,
    IAuditLogQueryReader auditLogQueryReader)
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    public async Task<Result<PagedResult<AuditLogDto>>> HandleAsync(
        int pageNumber,
        int pageSize,
        string? category,
        string? date,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return Result.Failure<PagedResult<AuditLogDto>>(IdentityErrors.InvalidSession);
        }

        var clampedPage = pageNumber < 1 ? 1 : pageNumber;
        var clampedSize = Math.Clamp(pageSize, 1, 30);

        IReadOnlyCollection<string>? actions = category?.Trim().ToLowerInvariant() switch
        {
            "auth" => [AuditActions.Login, AuditActions.SessionRefreshed, AuditActions.SessionLoggedOut],
            "password" =>
            [
                AuditActions.PasswordChanged,
                AuditActions.PasswordReset,
                AuditActions.PasswordRecoveryRequested,
                AuditActions.RecoveryOtpVerified,
                AuditActions.AccountActivated
            ],
            "session" =>
            [
                AuditActions.SessionRevoked,
                AuditActions.UserSessionsRevoked,
                AuditActions.RefreshTokenReuseDetected
            ],
            "profile" => [AuditActions.ProfileUpdated, AuditActions.UserUpdated],
            _ => null
        };

        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParseExact(
                    date.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return Result.Failure<PagedResult<AuditLogDto>>(IdentityErrors.InvalidAuditLogFilter);
            }

            var localStart = parsedDate.ToDateTime(TimeOnly.MinValue);
            fromUtc = DateTime.SpecifyKind(localStart - VietnamUtcOffset, DateTimeKind.Utc);
            toUtc = fromUtc.Value.AddDays(1);
        }

        var query = new GetAuditLogsQuery(
            ActorUserId: userId,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            PageNumber: clampedPage,
            PageSize: clampedSize,
            SortBy: "timestamp",
            SortOrder: "desc",
            Actions: actions);

        var result = await auditLogQueryReader.GetAuditLogsAsync(query, cancellationToken);
        return Result.Success(result);
    }
}

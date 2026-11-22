using UGTM.Modules.Identity.Application.AuditLogs.GetAuditLogs;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.Identity.Application.Profiles.GetMyActivity;

public sealed class GetMyActivitySummaryHandler(
    ICurrentUser currentUser,
    IAuditLogQueryReader auditLogQueryReader,
    IClock clock)
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    public async Task<Result<UserActivitySummaryDto>> HandleAsync(
        int days,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return Result.Failure<UserActivitySummaryDto>(IdentityErrors.InvalidSession);
        }

        var clampedDays = Math.Clamp(days, 7, 365);
        var localToday = (clock.UtcNow + VietnamUtcOffset).Date;
        var startLocal = localToday.AddDays(-(clampedDays - 1));
        var fromUtc = DateTime.SpecifyKind(startLocal - VietnamUtcOffset, DateTimeKind.Utc);

        var summary = await auditLogQueryReader.GetUserActivitySummaryAsync(
            userId,
            fromUtc,
            VietnamUtcOffset,
            clock.UtcNow,
            cancellationToken);

        return Result.Success(summary);
    }
}

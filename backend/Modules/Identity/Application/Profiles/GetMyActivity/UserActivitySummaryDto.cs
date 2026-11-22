namespace UGTM.Modules.Identity.Application.Profiles.GetMyActivity;

public sealed record UserActivityDayDto(
    string Date,
    int TotalCount,
    int SuccessCount,
    int AlertCount);

public sealed record UserActivitySummaryDto(
    IReadOnlyList<UserActivityDayDto> Days,
    int TotalEvents,
    int ActiveDaysCount,
    int RecentAlertCount7d);

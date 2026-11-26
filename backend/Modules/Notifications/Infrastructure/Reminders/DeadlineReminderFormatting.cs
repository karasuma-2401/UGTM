using System.Globalization;
using UGTM.Modules.AcademicPeriods.Contracts.Reminders;
using UGTM.Modules.TopicCatalog.Contracts.Reminders;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

internal static class DeadlineReminderFormatting
{
    public static OpenBatchStageReminderDto? GetStage(OpenBatchReminderSnapshotDto batch, string stageCode)
        => batch.Stages.FirstOrDefault(s => string.Equals(s.StageCode, stageCode, StringComparison.OrdinalIgnoreCase));

    public static string GetTopicTitle(TopicBatchReminderSnapshotDto topics, Guid topicId)
        => topics.TopicsById.TryGetValue(topicId, out var topic) ? topic.TitleVi : "Đề tài KLTN";

    public static int? MatchSmallestThreshold(DateTime deadlineUtc, DateTime nowUtc, int[] sortedThresholds)
    {
        if (deadlineUtc <= nowUtc)
        {
            return null;
        }

        var remaining = deadlineUtc - nowUtc;
        foreach (var threshold in sortedThresholds)
        {
            if (remaining <= TimeSpan.FromHours(threshold))
            {
                return threshold;
            }
        }

        return null;
    }

    public static bool IsWithinWindow(DateTime deadlineUtc, DateTime nowUtc, int maxHours)
        => deadlineUtc > nowUtc && deadlineUtc - nowUtc <= TimeSpan.FromHours(maxHours);

    public static DateTime EstimateCouncilStartUtc(DateOnly defenseDate, string timeSlot)
    {
        var startTime = new TimeOnly(8, 0);
        if (!string.IsNullOrWhiteSpace(timeSlot))
        {
            var token = timeSlot.Split(['-', '–', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (TimeOnly.TryParseExact(token, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                startTime = parsed;
            }
        }

        var localVn = defenseDate.ToDateTime(startTime, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(localVn.AddHours(-7), DateTimeKind.Utc);
    }

    public static string FormatVn(DateTime utc)
        => utc.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "KLTN";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)].TrimEnd()}…";
    }
}

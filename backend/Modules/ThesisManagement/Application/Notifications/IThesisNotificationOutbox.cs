using System.Globalization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Notifications;

public interface IThesisNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}

public static class ThesisNotificationFormatting
{
    public static IReadOnlyList<Guid> GetConfirmedStudentIds(StudentGroup group)
        => group.Members
            .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
            .Select(m => m.StudentId)
            .Distinct()
            .ToArray();

    public static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "KLTN";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)].TrimEnd()}…";
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.##} MB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1.0, bytes / 1024.0):0.#} KB");
    }

    public static string FormatVietnamTime(DateTime utc)
        => utc.AddHours(7).ToString("HH:mm 'ngày' dd/MM/yyyy", CultureInfo.InvariantCulture);
}

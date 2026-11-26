using System.Globalization;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.Notifications;

public interface IDefenseCouncilNotificationOutbox
{
    void Enqueue(NotificationOutboxEntry entry, DateTime occurredAtUtc);
}

public static class DefenseCouncilNotificationFormatting
{
    public static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "KLTN";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..(maxLength - 1)].TrimEnd()}…";
    }

    public static string FormatCouncilRole(CouncilMemberRole role) => role switch
    {
        CouncilMemberRole.Chairman => "Chủ tịch Hội đồng",
        CouncilMemberRole.Secretary => "Thư ký Hội đồng",
        _ => "Ủy viên Hội đồng"
    };

    public static string FormatSchedule(DateOnly date, string timeSlot, string room)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{date:dd/MM/yyyy} • {timeSlot} • Phòng {room}");
}

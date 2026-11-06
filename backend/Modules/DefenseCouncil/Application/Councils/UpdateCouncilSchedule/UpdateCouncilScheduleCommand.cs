namespace UGTM.Modules.DefenseCouncil.Application.Councils.UpdateCouncilSchedule;

public sealed record UpdateCouncilScheduleCommand(
    Guid CouncilId,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room);

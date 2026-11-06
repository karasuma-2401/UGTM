namespace UGTM.Modules.DefenseCouncil.Application.Councils.UnlockCouncilMinute;

public sealed record UnlockCouncilMinuteCommand(
    Guid AssignmentId,
    string OverrideReason);

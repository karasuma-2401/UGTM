namespace UGTM.Modules.DefenseCouncil.Application.Councils.LockCouncilMinute;

public sealed record LockCouncilMinuteCommand(
    Guid AssignmentId,
    string Password);

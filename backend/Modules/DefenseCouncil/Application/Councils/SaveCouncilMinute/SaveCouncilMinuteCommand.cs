namespace UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;

public sealed record SaveCouncilMinuteCommand(
    Guid AssignmentId,
    string? SecretaryMarking = null,
    string? TargetComponentCode = null,
    string? StudentFeedback = null,
    string? ChairmanConclusion = null,
    bool IsPreview = false,
    string? Reason = null);

namespace UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;

public sealed record CreateProgressMilestoneCommand(
    Guid GroupId,
    string? Title,
    string? Description,
    DateTime DeadlineUtc);

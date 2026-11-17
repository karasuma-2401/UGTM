namespace UGTM.Modules.ThesisManagement.Application.ProgressMilestones.Dtos;

public sealed record ProgressMilestoneDto(
    Guid Id,
    Guid GroupId,
    string Title,
    string? Description,
    DateTime DeadlineUtc,
    Guid CreatedBy,
    DateTime CreatedAtUtc);

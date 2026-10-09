using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class ProgressReport
{
    public Guid Id { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid GroupId { get; set; }
    public int CurrentVersionNo { get; set; } = 1;
    public ProgressReportStatus Status { get; set; } = ProgressReportStatus.Submitted;
    public string? StudentNote { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? AdvisorFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public ProgressMilestone Milestone { get; set; } = null!;
    public StudentGroup Group { get; set; } = null!;
}

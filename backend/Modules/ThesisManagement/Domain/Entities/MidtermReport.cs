using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class MidtermReport
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public int CurrentVersionNo { get; set; } = 1;
    public MidtermReportStatus Status { get; set; } = MidtermReportStatus.Submitted;
    public string? AdvisorFeedback { get; set; }
    public bool? AllowedToContinue { get; set; }
    public string? StopReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
}

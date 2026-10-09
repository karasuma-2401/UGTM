using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class FinalReport
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public FinalReportVersionType VersionType { get; set; }
    public FinalReportStatus Status { get; set; } = FinalReportStatus.Submitted;
    public DateTime SubmittedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
}

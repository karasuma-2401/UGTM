using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class FinalSubmission
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public FinalSubmissionStatus Status { get; set; } = FinalSubmissionStatus.NotSubmitted;
    public int CurrentVersionNo { get; set; } = 1;
    public string? AbstractVi { get; set; }
    public string? AbstractEn { get; set; }
    public string? KeywordsVi { get; set; }
    public string? KeywordsEn { get; set; }
    public Guid? AdvisorConfirmedBy { get; set; }
    public DateTime? AdvisorConfirmedAt { get; set; }
    public string? FacultyFeedback { get; set; }
    public Guid? ArchivedConfirmedBy { get; set; }
    public DateTime? ArchivedConfirmedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
}

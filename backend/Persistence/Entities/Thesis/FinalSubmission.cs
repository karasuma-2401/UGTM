using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Thesis;

public class FinalSubmission
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public FinalSubmissionStatus Status { get; set; } = FinalSubmissionStatus.NotSubmitted;
    public Guid? ArchivedConfirmedBy { get; set; }
    public DateTime? ArchivedConfirmedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User? ArchivedConfirmedByUser { get; set; }
}

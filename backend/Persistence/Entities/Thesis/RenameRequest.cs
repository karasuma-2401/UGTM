using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Thesis;

public class RenameRequest
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string OldTitleVi { get; set; } = null!;
    public string NewTitleVi { get; set; } = null!;
    public string? OldTitleEn { get; set; }
    public string? NewTitleEn { get; set; }
    public string Reason { get; set; } = null!;
    public RenameRequestStatus Status { get; set; } = RenameRequestStatus.Created;
    public DateTime? AdvisorConfirmedAt { get; set; }
    public DateTime? FacultyApprovedAt { get; set; }
    public DateTime? AaApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
}

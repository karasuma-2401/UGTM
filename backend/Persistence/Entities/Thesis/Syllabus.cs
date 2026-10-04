using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Thesis;

public class Syllabus
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public int CurrentVersionNo { get; set; } = 1;
    public SyllabusStatus Status { get; set; } = SyllabusStatus.Submitted;
    public string? FacultyFeedback { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User? Reviewer { get; set; }
}

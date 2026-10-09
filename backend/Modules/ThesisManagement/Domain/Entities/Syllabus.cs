using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

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
}

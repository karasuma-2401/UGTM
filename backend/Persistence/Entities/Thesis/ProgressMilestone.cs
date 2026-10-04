using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence.Entities.Thesis;

public class ProgressMilestone
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime Deadline { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ProgressReport> ProgressReports { get; set; } = [];
}

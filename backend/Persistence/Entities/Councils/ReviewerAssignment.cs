using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence.Entities.Councils;

public class ReviewerAssignment
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
    public ReviewerEvaluation? Evaluation { get; set; }
}

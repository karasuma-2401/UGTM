
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class ReviewerAssignment
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; }
    public int AssignmentOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ReviewerEvaluation? Evaluation { get; set; }
}

using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class Council
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string CouncilCode { get; set; } = null!;
    public DateOnly DefenseDate { get; set; }
    public string TimeSlot { get; set; } = null!;
    public string Room { get; set; } = null!;
    public CouncilStatus Status { get; set; } = CouncilStatus.Draft;
    public string? DecisionNumber { get; set; }
    public DateOnly? SignedDate { get; set; }
    public string? DecisionFileUrl { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ICollection<CouncilMember> Members { get; set; } = [];
    public ICollection<CouncilAssignment> Assignments { get; set; } = [];
}

using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class CouncilMinute
{
    public Guid Id { get; set; }
    public Guid CouncilAssignmentId { get; set; }
    public string? GeneralFeedback { get; set; }
    public string? RequiredRevisions { get; set; }
    public CouncilMinuteLockStatus LockStatus { get; set; } = CouncilMinuteLockStatus.Open;
    public DateTime? LockedAt { get; set; }
    public Guid? LockedBy { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public Guid? UnlockedBy { get; set; }

    public CouncilAssignment Assignment { get; set; } = null!;
}

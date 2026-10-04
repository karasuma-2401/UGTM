using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Councils;

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

    public CouncilAssignment Assignment { get; set; } = null!;
    public User? LockedByUser { get; set; }
}

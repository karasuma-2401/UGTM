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
    public uint Version { get; set; }

    public CouncilAssignment Assignment { get; set; } = null!;

    public bool TryLock(Guid actorId, DateTime atUtc)
    {
        if (LockStatus == CouncilMinuteLockStatus.Locked) return false;
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Lock time must be UTC.", nameof(atUtc));

        LockStatus = CouncilMinuteLockStatus.Locked;
        LockedBy = actorId;
        LockedAt = atUtc;
        return true;
    }

    public bool TryUnlock(Guid actorId, string reason, DateTime atUtc)
    {
        if (LockStatus != CouncilMinuteLockStatus.Locked) return false;
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID cannot be empty.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Override reason is required.", nameof(reason));
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Unlock time must be UTC.", nameof(atUtc));

        LockStatus = CouncilMinuteLockStatus.UnlockedByOverride;
        OverrideReason = reason.Trim();
        UnlockedBy = actorId;
        UnlockedAt = atUtc;
        LockedAt = null;
        LockedBy = null;
        return true;
    }
}

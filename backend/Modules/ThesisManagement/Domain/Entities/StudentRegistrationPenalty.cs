namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class StudentRegistrationPenalty
{
    public const int DefaultMaxViolations = 3;

    private StudentRegistrationPenalty() { }

    public StudentRegistrationPenalty(Guid studentId)
    {
        if (studentId == Guid.Empty)
        {
            throw new ArgumentException("Student identifier must be nonempty.", nameof(studentId));
        }

        StudentId = studentId;
        ViolationCount = 0;
        IsRegistrationBlocked = false;
    }

    public Guid StudentId { get; private set; }
    public int ViolationCount { get; private set; }
    public bool IsRegistrationBlocked { get; private set; }
    public DateTime? LastViolationAtUtc { get; private set; }
    public DateTime? BlockedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public static StudentRegistrationPenalty Create(Guid studentId) => new(studentId);

    public void RecordViolation(DateTime occurredAtUtc, int maxViolations = DefaultMaxViolations)
    {
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Occurred time must be UTC.", nameof(occurredAtUtc));
        }

        ViolationCount++;
        LastViolationAtUtc = occurredAtUtc;

        if (ViolationCount >= maxViolations && !IsRegistrationBlocked)
        {
            IsRegistrationBlocked = true;
            BlockedAtUtc = occurredAtUtc;
        }
    }

    public void Unblock()
    {
        IsRegistrationBlocked = false;
        BlockedAtUtc = null;
    }
}

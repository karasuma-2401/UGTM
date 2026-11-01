using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class StudentGroup : AggregateRoot<Guid>
{
    private readonly List<StudentGroupMember> _members = [];

    private StudentGroup() { }

    public StudentGroup(Guid id, Guid topicId, DateTime createdAt) : base(id)
    {
        if (id == Guid.Empty || topicId == Guid.Empty)
        {
            throw new ArgumentException("Group and topic identifiers must be nonempty.");
        }

        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        }

        TopicId = topicId;
        CreatedAt = createdAt;
    }

    public Guid TopicId { get; private set; }
    public StudentGroupStatus Status { get; private set; } = StudentGroupStatus.Registered;
    public DateTime CreatedAt { get; private set; }
    public uint Version { get; private set; }
    public bool AllowSyllabusResubmission { get; private set; }
    public IReadOnlyCollection<StudentGroupMember> Members => _members.AsReadOnly();

    public void EnableSyllabusResubmission()
    {
        AllowSyllabusResubmission = true;
    }

    public void ConsumeSyllabusResubmission()
    {
        AllowSyllabusResubmission = false;
    }

    public StudentGroupMember AddMember(Guid studentId, Guid batchId, DateTime joinedAt)
        => AddConfirmedMember(studentId, batchId, joinedAt);

    public StudentGroupMember AddConfirmedMember(Guid studentId, Guid batchId, DateTime joinedAt)
    {
        ValidateCanAddMember(studentId, batchId, joinedAt);

        var member = new StudentGroupMember(
            this,
            studentId,
            batchId,
            joinedAt,
            invitedByStudentId: null,
            confirmationStatus: MemberConfirmationStatus.Confirmed,
            confirmationTokenHash: null,
            confirmationExpiresAtUtc: null,
            confirmedAtUtc: joinedAt);

        _members.Add(member);
        return member;
    }

    public StudentGroupMember AddPendingMember(
        Guid studentId,
        Guid batchId,
        Guid invitedByStudentId,
        string confirmationTokenHash,
        DateTime joinedAt,
        DateTime expiresAtUtc)
    {
        ValidateCanAddMember(studentId, batchId, joinedAt);

        if (invitedByStudentId == Guid.Empty)
        {
            throw new ArgumentException("Inviter student identifier must be nonempty.", nameof(invitedByStudentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationTokenHash);

        if (expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= joinedAt)
        {
            throw new ArgumentException("Confirmation expiration time must be in UTC and after joining time.", nameof(expiresAtUtc));
        }

        var member = new StudentGroupMember(
            this,
            studentId,
            batchId,
            joinedAt,
            invitedByStudentId: invitedByStudentId,
            confirmationStatus: MemberConfirmationStatus.Pending,
            confirmationTokenHash: confirmationTokenHash,
            confirmationExpiresAtUtc: expiresAtUtc,
            confirmedAtUtc: null);

        _members.Add(member);
        return member;
    }

    public bool ConfirmMember(Guid studentId, DateTime confirmedAtUtc)
    {
        var member = _members.FirstOrDefault(m => m.StudentId == studentId);
        if (member is null)
        {
            return false;
        }

        member.Confirm(confirmedAtUtc);
        return true;
    }

    public bool RemoveMember(Guid studentId)
    {
        var member = _members.FirstOrDefault(m => m.StudentId == studentId);
        if (member is null)
        {
            return false;
        }

        return _members.Remove(member);
    }

    private void ValidateCanAddMember(Guid studentId, Guid batchId, DateTime joinedAt)
    {
        if (studentId == Guid.Empty || batchId == Guid.Empty || joinedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Student and batch identifiers must be nonempty and joining time must be UTC.");
        }

        if (Status != StudentGroupStatus.Registered)
        {
            throw new InvalidOperationException("Membership can only change during registration.");
        }

        if (_members.Any(member => member.StudentId == studentId))
        {
            throw new InvalidOperationException("The student is already in this group.");
        }

        if (_members.Count >= 2)
        {
            throw new InvalidOperationException("A thesis group may contain at most two students.");
        }

        if (_members.Any(member => member.BatchId != batchId))
        {
            throw new InvalidOperationException("All members must belong to the same academic batch.");
        }
    }

    public bool TrySubmitSyllabus()
    {
        if (Status is not (StudentGroupStatus.Registered or StudentGroupStatus.SyllabusRevisionRequired))
        {
            return false;
        }

        Status = StudentGroupStatus.SyllabusSubmitted;
        return true;
    }

    public bool TryApproveSyllabus()
    {
        if (Status != StudentGroupStatus.SyllabusSubmitted)
        {
            return false;
        }

        Status = StudentGroupStatus.InProgress;
        return true;
    }

    public bool TryOpenSyllabusRevision()
    {
        if (Status != StudentGroupStatus.SyllabusSubmitted)
        {
            return false;
        }

        Status = StudentGroupStatus.SyllabusRevisionRequired;
        return true;
    }

    public bool TrySubmitMidtermReport()
    {
        if (Status != StudentGroupStatus.InProgress)
        {
            return false;
        }

        Status = StudentGroupStatus.MidtermSubmitted;
        return true;
    }

    public bool TryApproveMidtermReport()
    {
        if (Status != StudentGroupStatus.MidtermSubmitted)
        {
            return false;
        }

        Status = StudentGroupStatus.InProgress;
        return true;
    }

    public bool TryOpenMidtermReportRevision()
    {
        if (Status != StudentGroupStatus.MidtermSubmitted)
        {
            return false;
        }

        Status = StudentGroupStatus.InProgress;
        return true;
    }

    public bool TrySubmitFinalReport()
    {
        if (Status != StudentGroupStatus.InProgress)
        {
            return false;
        }

        Status = StudentGroupStatus.FinalReportSubmitted;
        return true;
    }

    public bool TryMakeReadyForReviewerAssignment()
    {
        if (Status != StudentGroupStatus.FinalReportSubmitted)
        {
            return false;
        }

        Status = StudentGroupStatus.ReadyForReviewerAssignment;
        return true;
    }

    public bool TryStop()
    {
        if (Status is StudentGroupStatus.Stopped
            or StudentGroupStatus.Defended
            or StudentGroupStatus.ScoreLocked
            or StudentGroupStatus.Completed)
        {
            return false;
        }

        Status = StudentGroupStatus.Stopped;
        return true;
    }
}

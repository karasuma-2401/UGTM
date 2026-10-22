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
    public IReadOnlyCollection<StudentGroupMember> Members => _members.AsReadOnly();

    public StudentGroupMember AddMember(Guid studentId, Guid batchId, DateTime joinedAt)
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

        var member = new StudentGroupMember(this, studentId, batchId, joinedAt);
        _members.Add(member);
        return member;
    }

    public bool TrySubmitSyllabus()
    {
        if (Status is not (StudentGroupStatus.Registered or StudentGroupStatus.SyllabusSubmitted))
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

    public bool TrySubmitMidtermReport()
    {
        if (Status is not (StudentGroupStatus.InProgress or StudentGroupStatus.MidtermSubmitted))
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

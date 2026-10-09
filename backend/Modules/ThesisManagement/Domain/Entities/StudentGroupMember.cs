
namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class StudentGroupMember
{
    private StudentGroupMember() { }

    internal StudentGroupMember(StudentGroup group, Guid studentId, Guid batchId, DateTime joinedAt)
    {
        Id = Guid.NewGuid();
        Group = group;
        GroupId = group.Id;
        StudentId = studentId;
        BatchId = batchId;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid BatchId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    public StudentGroup Group { get; private set; } = null!;
}

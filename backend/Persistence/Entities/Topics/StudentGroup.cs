using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Topics;

public class StudentGroup
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public StudentGroupStatus Status { get; set; } = StudentGroupStatus.Registered;
    public DateTime CreatedAt { get; set; }

    public Topic Topic { get; set; } = null!;
    public ICollection<StudentGroupMember> Members { get; set; } = [];
}

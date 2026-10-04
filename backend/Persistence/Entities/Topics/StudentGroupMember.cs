using UGTM.Persistence.Entities.Academic;
using UGTM.Persistence.Entities.Identity;

namespace UGTM.Persistence.Entities.Topics;

public class StudentGroupMember
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid StudentId { get; set; }
    public Guid BatchId { get; set; }
    public DateTime JoinedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;
    public User Student { get; set; } = null!;
    public KltnBatch Batch { get; set; } = null!;
}

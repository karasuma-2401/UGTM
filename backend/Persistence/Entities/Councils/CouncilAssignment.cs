using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence.Entities.Councils;

public class CouncilAssignment
{
    public Guid Id { get; set; }
    public Guid CouncilId { get; set; }
    public Guid GroupId { get; set; }
    public int OrderNumber { get; set; } = 1;

    public Council Council { get; set; } = null!;
    public StudentGroup Group { get; set; } = null!;
    public ICollection<CouncilScore> Scores { get; set; } = [];
    public CouncilMinute? Minute { get; set; }
}

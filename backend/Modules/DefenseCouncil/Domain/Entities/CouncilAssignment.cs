
namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class CouncilAssignment
{
    public Guid Id { get; set; }
    public Guid CouncilId { get; set; }
    public Guid GroupId { get; set; }
    public int OrderNumber { get; set; } = 1;

    public Council Council { get; set; } = null!;
    public ICollection<CouncilScore> Scores { get; set; } = [];
    public CouncilMinute? Minute { get; set; }
}

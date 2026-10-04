using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Entities.Councils;

public class CouncilMember
{
    public Guid Id { get; set; }
    public Guid CouncilId { get; set; }
    public Guid UserId { get; set; }
    public CouncilMemberRole RoleInCouncil { get; set; }

    public Council Council { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<CouncilScore> Scores { get; set; } = [];
}

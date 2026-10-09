using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class CouncilMember
{
    public Guid Id { get; set; }
    public Guid CouncilId { get; set; }
    public Guid UserId { get; set; }
    public CouncilMemberRole RoleInCouncil { get; set; }

    public Council Council { get; set; } = null!;
    public ICollection<CouncilScore> Scores { get; set; } = [];
}

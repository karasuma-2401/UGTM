using UGTM.Persistence.Entities.Identity;

namespace UGTM.Persistence.Entities.Academic;

public class RoleScoreWeight
{
    public Guid Id { get; set; }
    public Guid ConfigId { get; set; }
    public int RoleId { get; set; }
    public decimal Weight { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }

    public ScoreWeightConfig Config { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public User UpdatedByUser { get; set; } = null!;
}

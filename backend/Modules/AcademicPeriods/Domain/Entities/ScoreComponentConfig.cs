using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public class ScoreComponentConfig
{
    public Guid Id { get; set; }
    public Guid ConfigId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public ScoreComponentSource Source { get; set; }
    public ScoreComponentApplicability Applicability { get; set; } = ScoreComponentApplicability.Always;
    public decimal Weight { get; set; }
    public bool IsRequired { get; set; } = true;
    public decimal? MinimumScore { get; set; }
    public bool RequiresApproval { get; set; }
    public int OrderIndex { get; set; }

    public ScoreWeightConfig Config { get; set; } = null!;
}

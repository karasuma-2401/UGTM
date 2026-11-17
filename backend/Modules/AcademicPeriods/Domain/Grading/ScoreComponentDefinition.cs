using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Grading;

public sealed record ScoreComponentDefinition(
    string ComponentCode,
    string ComponentName,
    ScoreComponentSource Source,
    ScoreComponentApplicability Applicability,
    decimal Weight,
    bool IsRequired,
    decimal? MinimumScore,
    bool RequiresApproval,
    int OrderIndex);

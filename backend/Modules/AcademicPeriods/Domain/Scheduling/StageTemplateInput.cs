using UGTM.Modules.AcademicPeriods.Domain.Enums;

namespace UGTM.Modules.AcademicPeriods.Domain.Scheduling;

public sealed record StageTemplateInput(
    int OrderIndex,
    string StageCode,
    string StageName,
    int StartOffsetDays,
    int EndOffsetDays,
    TimeOnly DefaultDeadlineLocalTime,
    DeadlineAdjustmentPolicy DeadlineAdjustmentPolicy = DeadlineAdjustmentPolicy.None);

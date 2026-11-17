using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class StageTemplateDefinition
{
    private StageTemplateDefinition() { }

    private StageTemplateDefinition(Guid templateVersionId, StageTemplateInput input)
    {
        Id = Guid.NewGuid();
        TemplateVersionId = templateVersionId;
        StageCode = input.StageCode.Trim();
        StageName = input.StageName.Trim();
        OrderIndex = input.OrderIndex;
        StartOffsetDays = input.StartOffsetDays;
        EndOffsetDays = input.EndOffsetDays;
        DefaultDeadlineLocalTime = input.DefaultDeadlineLocalTime;
        DeadlineAdjustmentPolicy = input.DeadlineAdjustmentPolicy;
    }

    public Guid Id { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public string StageCode { get; private set; } = null!;
    public string StageName { get; private set; } = null!;
    public int OrderIndex { get; private set; }
    public int StartOffsetDays { get; private set; }
    public int EndOffsetDays { get; private set; }
    public TimeOnly DefaultDeadlineLocalTime { get; private set; }
    public DeadlineAdjustmentPolicy DeadlineAdjustmentPolicy { get; private set; }

    public ScheduleTemplateVersion TemplateVersion { get; private set; } = null!;

    internal static StageTemplateDefinition Create(Guid templateVersionId, StageTemplateInput input)
    {
        if (templateVersionId == Guid.Empty)
        {
            throw new ArgumentException("Template version identifier must be nonempty.", nameof(templateVersionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(input.StageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StageName);
        if (input.StageCode.Trim().Length > 100 || input.StageName.Trim().Length > 150)
        {
            throw new ArgumentException("Stage code or name exceeds its maximum length.", nameof(input));
        }

        return new StageTemplateDefinition(templateVersionId, input);
    }
}

using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class ScheduleTemplateVersion : AggregateRoot<Guid>
{
    private readonly List<StageTemplateDefinition> _stageDefinitions = [];

    private ScheduleTemplateVersion() { }

    private ScheduleTemplateVersion(
        Guid id,
        string templateCode,
        int versionNo,
        string name,
        string timeZoneId,
        IEnumerable<StageTemplateInput> definitions,
        Guid? createdBy,
        DateTime createdAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Identifier must be nonempty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (templateCode.Trim().Length > 100 || name.Trim().Length > 200)
        {
            throw new ArgumentException("Template code or name exceeds its maximum length.");
        }

        if (versionNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNo), "Version number must be positive.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation timestamp must be UTC.", nameof(createdAt));
        }

        Id = id;
        TemplateCode = templateCode.Trim();
        VersionNo = versionNo;
        Name = name.Trim();
        TimeZoneId = timeZoneId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;

        var values = definitions?.OrderBy(x => x.OrderIndex).ToArray()
            ?? throw new ArgumentNullException(nameof(definitions));
        ValidateDefinitions(values);
        _stageDefinitions.AddRange(values.Select(value => StageTemplateDefinition.Create(Id, value)));
    }

    public string TemplateCode { get; private set; } = null!;
    public int VersionNo { get; private set; }
    public string Name { get; private set; } = null!;
    public string TimeZoneId { get; private set; } = KltnBatch.DefaultTimeZoneId;
    public ConfigurationVersionStatus Status { get; private set; } = ConfigurationVersionStatus.Draft;
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? RetiredAt { get; private set; }
    public IReadOnlyCollection<StageTemplateDefinition> StageDefinitions => _stageDefinitions.AsReadOnly();

    public static ScheduleTemplateVersion CreateDraft(
        Guid id,
        string templateCode,
        int versionNo,
        string name,
        string timeZoneId,
        IEnumerable<StageTemplateInput> definitions,
        Guid createdBy,
        DateTime createdAt)
    {
        if (createdBy == Guid.Empty)
        {
            throw new ArgumentException("Creator identifier must be nonempty.", nameof(createdBy));
        }

        return new ScheduleTemplateVersion(id, templateCode, versionNo, name, timeZoneId, definitions, createdBy, createdAt);
    }

    public void Publish(Guid publishedBy, DateTime publishedAt)
    {
        if (Status != ConfigurationVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft schedule template can be published.");
        }

        if (publishedBy == Guid.Empty || publishedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Publisher must be nonempty and timestamp must be UTC.");
        }

        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        Status = ConfigurationVersionStatus.Active;
    }

    public void Retire(DateTime retiredAt)
    {
        if (Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("Only an active schedule template can be retired.");
        }

        if (retiredAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Retirement timestamp must be UTC.", nameof(retiredAt));
        }

        RetiredAt = retiredAt;
        Status = ConfigurationVersionStatus.Retired;
    }

    // When I wrote this, only God and I understood what I was doing.
    // Now, God only knows!
    // Good luck!
    
    public IReadOnlyList<StageSchedule> Materialize(DateOnly batchStartDate)
    {
        if (Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("Only an active schedule template can be materialized.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        return _stageDefinitions
            .OrderBy(x => x.OrderIndex)
            .Select(definition =>
            {
                if (definition.DeadlineAdjustmentPolicy != DeadlineAdjustmentPolicy.None)
                {
                    throw new InvalidOperationException("A working-day calendar is required for this deadline adjustment policy.");
                }

                var startDate = batchStartDate.AddDays(definition.StartOffsetDays);
                var endDate = batchStartDate.AddDays(definition.EndOffsetDays);
                var localDeadline = DateTime.SpecifyKind(endDate.ToDateTime(definition.DefaultDeadlineLocalTime), DateTimeKind.Unspecified);
                var deadlineAtUtc = TimeZoneInfo.ConvertTimeToUtc(localDeadline, timeZone);
                return new StageSchedule(definition.OrderIndex, definition.StageCode, definition.StageName, startDate, endDate, deadlineAtUtc);
            })
            .ToArray();
    }

    private static void ValidateDefinitions(IReadOnlyList<StageTemplateInput> definitions)
    {
        if (definitions.Count != KltnBatch.RequiredStageCount)
        {
            throw new ArgumentException("A schedule template must contain exactly seven stages.", nameof(definitions));
        }

        if (definitions.Select(x => x.StageCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definitions.Count ||
            definitions.Select(x => x.OrderIndex).Distinct().Count() != definitions.Count)
        {
            throw new ArgumentException("Template stage codes and order values must be unique.", nameof(definitions));
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            var current = definitions[index];
            if (current.OrderIndex != index + 1 || current.StartOffsetDays < 0 || current.EndOffsetDays < current.StartOffsetDays)
            {
                throw new ArgumentException("Template stages must have valid contiguous order and offsets.", nameof(definitions));
            }

            if ((index == 0 && current.StartOffsetDays != 0) ||
                (index > 0 && current.StartOffsetDays != definitions[index - 1].EndOffsetDays + 1))
            {
                throw new ArgumentException("Template stages must be contiguous and cannot overlap.", nameof(definitions));
            }
        }
    }
}

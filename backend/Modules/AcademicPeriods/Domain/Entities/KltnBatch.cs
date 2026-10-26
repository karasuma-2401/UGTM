using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class KltnBatch : AggregateRoot<Guid>
{
    public const int RequiredStageCount = 7;
    public const string DefaultTimeZoneId = "Asia/Ho_Chi_Minh";

    private readonly List<Stage> _stages = [];
    private readonly List<ScoreWeightConfig> _scoreConfigurations = [];

    private KltnBatch() { }

    private KltnBatch(
        Guid id,
        string batchName,
        string academicYear,
        int semester,
        DateOnly startDate,
        DateOnly endDate,
        string timeZoneId,
        Guid scheduleTemplateVersionId,
        Guid createdBy,
        DateTime createdAt) : base(id)
    {
        Id = RequireId(id, nameof(id));
        BatchName = RequireText(batchName, nameof(batchName), 100);
        AcademicYear = RequireText(academicYear, nameof(academicYear), 20);
        Semester = semester > 0
            ? semester
            : throw new ArgumentOutOfRangeException(nameof(semester), "Semester must be positive.");

        if (endDate <= startDate)
        {
            throw new ArgumentException("Batch end date must be after its start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        TimeZoneId = ValidateTimeZone(timeZoneId);
        ScheduleTemplateVersionId = RequireId(scheduleTemplateVersionId, nameof(scheduleTemplateVersionId));
        CreatedBy = RequireId(createdBy, nameof(createdBy));
        CreatedAt = RequireUtc(createdAt, nameof(createdAt));
        UpdatedBy = createdBy;
        UpdatedAt = createdAt;
    }

    public string BatchName { get; private set; } = null!;
    public string AcademicYear { get; private set; } = null!;
    public int Semester { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string TimeZoneId { get; private set; } = DefaultTimeZoneId;
    public Guid? ScheduleTemplateVersionId { get; private set; }
    public BatchStatus Status { get; private set; } = BatchStatus.Draft;
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public ScheduleTemplateVersion? ScheduleTemplateVersion { get; private set; }
    public IReadOnlyCollection<Stage> Stages => _stages.AsReadOnly();
    public IReadOnlyCollection<ScoreWeightConfig> ScoreConfigurations => _scoreConfigurations.AsReadOnly();

    public static KltnBatch Create(
        Guid id,
        string batchName,
        string academicYear,
        int semester,
        DateOnly startDate,
        DateOnly endDate,
        ScheduleTemplateVersion template,
        Guid createdBy,
        DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("A batch can only use an active schedule template.");
        }

        var batch = new KltnBatch(
            id,
            batchName,
            academicYear,
            semester,
            startDate,
            endDate,
            template.TimeZoneId,
            template.Id,
            createdBy,
            createdAt);

        batch.ReplaceSchedule(template.Materialize(startDate), null, createdBy, createdAt);
        return batch;
    }

    public void ReplaceSchedule(
        IEnumerable<StageSchedule> schedule,
        string? changeReason,
        Guid updatedBy,
        DateTime updatedAt)
    {
        EnsureScheduleCanBeReplaced(changeReason);

        var actorId = RequireId(updatedBy, nameof(updatedBy));
        var occurredAt = RequireUtc(updatedAt, nameof(updatedAt));
        var values = PrepareSchedule(schedule);

        _stages.Clear();
        _stages.AddRange(values.Select(value => Stage.Create(Id, value, TimeZoneId, Status == BatchStatus.Open)));
        UpdatedBy = actorId;
        UpdatedAt = occurredAt;
    }

    public void ValidateScheduleReplacement(
        IEnumerable<StageSchedule> schedule,
        string? changeReason)
    {
        EnsureScheduleCanBeReplaced(changeReason);
        _ = PrepareSchedule(schedule);
    }

    public void RemoveStagesForScheduleReplacement(string? changeReason)
    {
        EnsureScheduleCanBeReplaced(changeReason);
        _stages.Clear();
    }

    public Stage? GetStageOn(DateOnly date)
        => _stages.SingleOrDefault(stage => date >= stage.StartDate && date <= stage.EndDate);

    public void Open(ScoreWeightConfig activeScoreConfiguration, Guid updatedBy, DateTime updatedAt)
    {
        if (Status != BatchStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft batch can be opened.");
        }

        if (_stages.Count != RequiredStageCount)
        {
            throw new InvalidOperationException("A batch requires a complete seven-stage schedule before it can be opened.");
        }

        ArgumentNullException.ThrowIfNull(activeScoreConfiguration);
        if (activeScoreConfiguration.BatchId != Id || activeScoreConfiguration.Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("The batch requires its own active score configuration before it can be opened.");
        }

        Touch(updatedBy, updatedAt);
        Status = BatchStatus.Open;
    }

    public void Close(Guid updatedBy, DateTime updatedAt)
    {
        if (Status != BatchStatus.Open)
        {
            throw new InvalidOperationException("Only an open batch can be closed.");
        }

        Touch(updatedBy, updatedAt);
        Status = BatchStatus.Closed;
    }

    public void Archive(Guid updatedBy, DateTime updatedAt)
    {
        if (Status != BatchStatus.Closed)
        {
            throw new InvalidOperationException("Only a closed batch can be archived.");
        }

        Touch(updatedBy, updatedAt);
        Status = BatchStatus.Archived;
    }

    private void ValidateSchedule(IReadOnlyList<StageSchedule> schedule)
    {
        if (schedule.Count != RequiredStageCount)
        {
            throw new ArgumentException($"A batch schedule must contain exactly {RequiredStageCount} stages.", nameof(schedule));
        }

        if (schedule.Select(x => x.OrderIndex).Distinct().Count() != RequiredStageCount ||
            schedule.Where((x, index) => x.OrderIndex != index + 1).Any())
        {
            throw new ArgumentException("Stage order must be unique and contiguous from 1 to 7.", nameof(schedule));
        }

        if (schedule.Select(x => x.StageCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != RequiredStageCount)
        {
            throw new ArgumentException("Stage codes must be unique within a batch.", nameof(schedule));
        }

        if (schedule[0].StartDate != StartDate || schedule[^1].EndDate != EndDate)
        {
            throw new ArgumentException("The schedule must cover the complete batch date range.", nameof(schedule));
        }

        for (var index = 0; index < schedule.Count; index++)
        {
            var current = schedule[index];
            if (current.StartDate < StartDate || current.EndDate > EndDate || current.EndDate < current.StartDate)
            {
                throw new ArgumentException("Every stage must be contained in the batch date range.", nameof(schedule));
            }

            if (index > 0 && current.StartDate != schedule[index - 1].EndDate.AddDays(1))
            {
                throw new ArgumentException("Stages must be contiguous and cannot overlap.", nameof(schedule));
            }
        }
    }

    private void EnsureScheduleCanBeReplaced(string? changeReason)
    {
        if (Status is BatchStatus.Closed or BatchStatus.Archived)
        {
            throw new InvalidOperationException("The schedule of a closed or archived batch cannot be changed.");
        }

        if (Status == BatchStatus.Open && string.IsNullOrWhiteSpace(changeReason))
        {
            throw new ArgumentException("A reason is required when changing the schedule of an open batch.", nameof(changeReason));
        }
    }

    private StageSchedule[] PrepareSchedule(IEnumerable<StageSchedule> schedule)
    {
        var values = schedule?.OrderBy(x => x.OrderIndex).ToArray()
            ?? throw new ArgumentNullException(nameof(schedule));
        ValidateSchedule(values);
        return values;
    }

    private void Touch(Guid updatedBy, DateTime updatedAt)
    {
        UpdatedBy = RequireId(updatedBy, nameof(updatedBy));
        UpdatedAt = RequireUtc(updatedAt, nameof(updatedAt));
    }

    private static Guid RequireId(Guid value, string parameterName)
        => value != Guid.Empty ? value : throw new ArgumentException("Identifier must be nonempty.", parameterName);

    private static DateTime RequireUtc(DateTime value, string parameterName)
        => value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", parameterName);

    private static string RequireText(string value, string parameterName, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
    }

    private static string ValidateTimeZone(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return timeZoneId;
    }
}

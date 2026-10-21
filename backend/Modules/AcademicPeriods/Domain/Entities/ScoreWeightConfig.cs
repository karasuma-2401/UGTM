using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class ScoreWeightConfig : AggregateRoot<Guid>
{
    private readonly List<ScoreComponentConfig> _components = [];

    private ScoreWeightConfig() { }

    private ScoreWeightConfig(
        Guid id,
        Guid batchId,
        int versionNo,
        ScoreCalculationMethod calculationMethod,
        IEnumerable<ScoreComponentDefinition> components,
        Guid createdBy,
        DateTime createdAt,
        string? changeReason) : base(id)
    {
        Id = RequireId(id, nameof(id));
        BatchId = RequireId(batchId, nameof(batchId));
        VersionNo = versionNo > 0
            ? versionNo
            : throw new ArgumentOutOfRangeException(nameof(versionNo), "Version number must be positive.");
        CreatedBy = RequireId(createdBy, nameof(createdBy));
        CreatedAt = RequireUtc(createdAt, nameof(createdAt));
        UpdatedAt = createdAt;
        ChangeReason = NormalizeOptionalText(changeReason, 1000, nameof(changeReason));
        if (versionNo > 1 && ChangeReason is null)
        {
            throw new ArgumentException("A change reason is required after the first score configuration version.", nameof(changeReason));
        }

        ReplaceComponents(calculationMethod, components);
    }

    public Guid BatchId { get; private set; }
    public ConfigurationVersionStatus Status { get; private set; } = ConfigurationVersionStatus.Draft;
    public ScoreCalculationMethod CalculationMethod { get; private set; }
    public int VersionNo { get; private set; }
    public string? ChangeReason { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? RetiredAt { get; private set; }
    public uint Version { get; private set; }

    public KltnBatch Batch { get; private set; } = null!;
    public IReadOnlyCollection<ScoreComponentConfig> Components => _components.AsReadOnly();

    public static ScoreWeightConfig CreateDraft(
        Guid id,
        Guid batchId,
        int versionNo,
        ScoreCalculationMethod calculationMethod,
        IEnumerable<ScoreComponentDefinition> components,
        Guid createdBy,
        DateTime createdAt,
        string? changeReason = null)
        => new(id, batchId, versionNo, calculationMethod, components, createdBy, createdAt, changeReason);

    public void UpdateDraft(
        ScoreCalculationMethod calculationMethod,
        IEnumerable<ScoreComponentDefinition> components,
        DateTime updatedAt,
        string? changeReason)
    {
        EnsureDraft();
        var normalizedChangeReason = NormalizeOptionalText(changeReason, 1000, nameof(changeReason));
        if (VersionNo > 1 && normalizedChangeReason is null)
        {
            throw new ArgumentException("A change reason is required after the first score configuration version.", nameof(changeReason));
        }

        ReplaceComponents(calculationMethod, components);
        UpdatedAt = RequireUtc(updatedAt, nameof(updatedAt));
        ChangeReason = normalizedChangeReason;
    }

    public ScoreWeightConfig CloneAsNextDraft(Guid newId, Guid createdBy, DateTime createdAt, string changeReason)
    {
        if (Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("Only an active score configuration can be cloned.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(changeReason);
        return CreateDraft(
            newId,
            BatchId,
            checked(VersionNo + 1),
            CalculationMethod,
            _components.OrderBy(x => x.OrderIndex).Select(x => x.ToDefinition()),
            createdBy,
            createdAt,
            changeReason);
    }

    public void Publish(Guid publishedBy, DateTime publishedAt)
    {
        EnsureDraft();
        ValidateConfiguration(CalculationMethod, _components.Select(x => x.ToDefinition()).ToArray());
        PublishedBy = RequireId(publishedBy, nameof(publishedBy));
        PublishedAt = RequireUtc(publishedAt, nameof(publishedAt));
        UpdatedAt = publishedAt;
        Status = ConfigurationVersionStatus.Active;
    }

    public void Retire(DateTime retiredAt)
    {
        if (Status != ConfigurationVersionStatus.Active)
        {
            throw new InvalidOperationException("Only an active score configuration can be retired.");
        }

        RetiredAt = RequireUtc(retiredAt, nameof(retiredAt));
        Status = ConfigurationVersionStatus.Retired;
    }

    private void ReplaceComponents(
        ScoreCalculationMethod calculationMethod,
        IEnumerable<ScoreComponentDefinition> components)
    {
        var values = components?.OrderBy(x => x.OrderIndex).ToArray()
            ?? throw new ArgumentNullException(nameof(components));
        ValidateConfiguration(calculationMethod, values);

        _components.Clear();
        _components.AddRange(values.Select(value => ScoreComponentConfig.Create(Id, value)));
        CalculationMethod = calculationMethod;
    }

    private static void ValidateConfiguration(
        ScoreCalculationMethod calculationMethod,
        IReadOnlyCollection<ScoreComponentDefinition> components)
    {
        if (components.Count == 0)
        {
            throw new ArgumentException("At least one score component is required.", nameof(components));
        }

        if (components.Select(x => x.ComponentCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != components.Count)
        {
            throw new ArgumentException("Score component codes must be unique.", nameof(components));
        }

        if (components.Select(x => x.OrderIndex).Distinct().Count() != components.Count || components.Any(x => x.OrderIndex <= 0))
        {
            throw new ArgumentException("Score component order must be positive and unique.", nameof(components));
        }

        foreach (var component in components)
        {
            ScoreComponentConfig.Validate(component);
        }

        ScoreWeightRules.Validate(calculationMethod, components.Select(x => x.Weight));
    }

    private void EnsureDraft()
    {
        if (Status != ConfigurationVersionStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft score configuration can be edited or published.");
        }
    }

    private static Guid RequireId(Guid value, string parameterName)
        => value != Guid.Empty ? value : throw new ArgumentException("Identifier must be nonempty.", parameterName);

    private static DateTime RequireUtc(DateTime value, string parameterName)
        => value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("Timestamp must be UTC.", parameterName);

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"Value cannot exceed {maxLength} characters.", parameterName);
    }
}

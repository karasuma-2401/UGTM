using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Grading;

namespace UGTM.Modules.AcademicPeriods.Domain.Entities;

public sealed class ScoreComponentConfig
{
    private ScoreComponentConfig() { }

    private ScoreComponentConfig(Guid configId, ScoreComponentDefinition definition)
    {
        Id = Guid.NewGuid();
        ConfigId = configId;
        ComponentCode = definition.ComponentCode.Trim();
        ComponentName = definition.ComponentName.Trim();
        Source = definition.Source;
        Applicability = definition.Applicability;
        Weight = definition.Weight;
        IsRequired = definition.IsRequired;
        MinimumScore = definition.MinimumScore;
        RequiresApproval = definition.RequiresApproval;
        OrderIndex = definition.OrderIndex;
    }

    public Guid Id { get; private set; }
    public Guid ConfigId { get; private set; }
    public string ComponentCode { get; private set; } = null!;
    public string ComponentName { get; private set; } = null!;
    public ScoreComponentSource Source { get; private set; }
    public ScoreComponentApplicability Applicability { get; private set; } = ScoreComponentApplicability.Always;
    public decimal Weight { get; private set; }
    public bool IsRequired { get; private set; } = true;
    public decimal? MinimumScore { get; private set; }
    public bool RequiresApproval { get; private set; }
    public int OrderIndex { get; private set; }

    public ScoreWeightConfig Config { get; private set; } = null!;

    internal static ScoreComponentConfig Create(Guid configId, ScoreComponentDefinition definition)
    {
        if (configId == Guid.Empty)
        {
            throw new ArgumentException("Configuration identifier must be nonempty.", nameof(configId));
        }

        Validate(definition);
        return new ScoreComponentConfig(configId, definition);
    }

    internal static void Validate(ScoreComponentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.ComponentCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.ComponentName);

        if (definition.ComponentCode.Trim().Length > 100 || definition.ComponentName.Trim().Length > 200)
        {
            throw new ArgumentException("Score component code or name exceeds its maximum length.", nameof(definition));
        }

        if (definition.Weight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Score component weight cannot be negative.");
        }

        if (definition.MinimumScore is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Minimum score must be between 0 and 10.");
        }

        if (definition.OrderIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition), "Score component order must be positive.");
        }
    }

    internal ScoreComponentDefinition ToDefinition()
        => new(ComponentCode, ComponentName, Source, Applicability, Weight, IsRequired, MinimumScore, RequiresApproval, OrderIndex);
}

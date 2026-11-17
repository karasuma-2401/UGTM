namespace UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;

public sealed class ReviewGateDeadlineEnforcementOptions
{
    public const string SectionName = "ThesisManagement:ReviewGateDeadlineEnforcement";

    public int PollingIntervalSeconds { get; init; } = 60;

    public int BatchSize { get; init; } = 100;
}

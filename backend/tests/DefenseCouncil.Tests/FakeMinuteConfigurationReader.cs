using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;

namespace UGTM.Modules.DefenseCouncil.Tests;

internal sealed class FakeMinuteConfigurationReader : IScoreSummaryConfigurationReader
{
    public Func<Guid, ScoreSummaryConfigurationDto?> Factory { get; set; } = ForBatch;
    public Task<ScoreSummaryConfigurationDto?> GetAsync(Guid batchId, CancellationToken token = default) => Task.FromResult(Factory(batchId));
    public Task<ScoreSummaryConfigurationDto?> LockAsync(Guid batchId, CancellationToken token = default) => GetAsync(batchId, token);

    public static ScoreSummaryConfigurationDto ForBatch(Guid batchId) => new(Guid.NewGuid(), batchId, 1, 1, "WeightedAverage", true,
    [
        Component("ADVISOR", "Advisor", 2, 1), Component("REVIEWER", "Reviewer", 2, 2),
        Component("COUNCIL_CHAIRMAN", "CouncilChairman", 1, 3), Component("COUNCIL_SECRETARY", "CouncilSecretary", 1, 4),
        Component("COUNCIL_MEMBER", "CouncilMember", 1, 5, "ReviewerOutsideCouncil")
    ]);

    public static ScoreSummaryComponentDefinition Component(string code, string source, decimal weight, int order, string applicability = "Always")
        => new(Guid.NewGuid(), code, code, source, applicability, weight, true, null, false, order);
}

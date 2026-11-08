using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class CouncilDefenseEligibilityTests
{
    [Fact]
    public async Task Scheduling_uses_the_shared_eligibility_decision_for_each_distinct_group()
    {
        var approved = Guid.NewGuid();
        var belowConfiguredMinimum = Guid.NewGuid();
        var invalidBinding = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var eligibility = new FakeEligibility(approved, belowConfiguredMinimum, invalidBinding);
        await using var db = new DefenseCouncilDbContext(new DbContextOptionsBuilder<DefenseCouncilDbContext>().Options);
        var repository = new CouncilRepository(db, eligibility);

        var result = await repository.GetDefenseEligibleGroupIdsAsync(
            [approved, belowConfiguredMinimum, invalidBinding, missing, approved]);

        Assert.Equal([approved], result);
        Assert.Equal(4, eligibility.Calls.Count);
    }

    private sealed class FakeEligibility(Guid approved, Guid belowMinimum, Guid invalidBinding)
        : IReviewerDefenseEligibilityContract
    {
        public List<Guid> Calls { get; } = [];

        public Task<ReviewerDefenseEligibilityContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            Calls.Add(groupId);
            if (groupId != approved && groupId != belowMinimum && groupId != invalidBinding)
                return Task.FromResult<ReviewerDefenseEligibilityContractDto?>(null);

            var reason = groupId == belowMinimum ? ReviewerDefenseEligibilityReason.ScoreBelowMinimum
                : groupId == invalidBinding ? ReviewerDefenseEligibilityReason.BindingInvalid
                : (ReviewerDefenseEligibilityReason?)null;
            return Task.FromResult<ReviewerDefenseEligibilityContractDto?>(new(
                groupId, Guid.NewGuid(), "Open", Guid.NewGuid(), Guid.NewGuid(), 1,
                Guid.NewGuid(), 1, 5m, groupId == approved, reason));
        }

        public Task<ReviewerDefenseEligibilityContractDto?> LockForSchedulingAsync(Guid groupId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

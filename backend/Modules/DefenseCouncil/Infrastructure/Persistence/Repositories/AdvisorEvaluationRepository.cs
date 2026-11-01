using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class AdvisorEvaluationRepository(DefenseCouncilDbContext context) : IAdvisorEvaluationRepository
{
    public Task<AdvisorEvaluation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.AdvisorEvaluations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public Task<AdvisorEvaluation?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => context.AdvisorEvaluations.SingleOrDefaultAsync(value => value.GroupId == groupId, cancellationToken);

    public void Add(AdvisorEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        context.AdvisorEvaluations.Add(evaluation);
    }
}

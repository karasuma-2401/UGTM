using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class AdvisorEvaluationIdempotencyRepository(DefenseCouncilDbContext context)
    : IAdvisorEvaluationIdempotencyRepository
{
    public Task<AdvisorEvaluationIdempotencyRecord?> GetAsync(
        Guid actorId,
        string operation,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
        => context.AdvisorEvaluationIdempotencyRecords.SingleOrDefaultAsync(value =>
            value.ActorId == actorId
            && value.Operation == operation
            && value.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public void Add(AdvisorEvaluationIdempotencyRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        context.AdvisorEvaluationIdempotencyRecords.Add(record);
    }
}

using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Domain.Repositories;

public interface IAdvisorEvaluationIdempotencyRepository
{
    Task<AdvisorEvaluationIdempotencyRecord?> GetAsync(
        Guid actorId,
        string operation,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);

    void Add(AdvisorEvaluationIdempotencyRecord record);
}

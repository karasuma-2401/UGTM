using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IReviewGateConfigurationRepository
{
    Task<ReviewGateConfiguration?> GetByBatchAndGateAsync(
        Guid batchId,
        ReviewGateType gateType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewGateConfiguration>> GetByBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    void Add(ReviewGateConfiguration configuration);
}

using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ReviewGateConfigurationRepository(ThesisManagementDbContext context)
    : IReviewGateConfigurationRepository
{
    public Task<ReviewGateConfiguration?> GetByBatchAndGateAsync(
        Guid batchId,
        ReviewGateType gateType,
        CancellationToken cancellationToken = default)
        => context.ReviewGateConfigurations.SingleOrDefaultAsync(
            configuration => configuration.BatchId == batchId && configuration.GateType == gateType,
            cancellationToken);

    public async Task<IReadOnlyList<ReviewGateConfiguration>> GetByBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
        => await context.ReviewGateConfigurations
            .AsNoTracking()
            .Where(configuration => configuration.BatchId == batchId)
            .OrderBy(configuration => configuration.GateType)
            .ToListAsync(cancellationToken);

    public void Add(ReviewGateConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        context.ReviewGateConfigurations.Add(configuration);
    }
}

using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SubmissionReviewRepository(ThesisManagementDbContext context) : ISubmissionReviewRepository
{
    public async Task<IReadOnlyList<SubmissionReview>> GetBySubmissionAsync(
        ReviewGateType gateType,
        Guid submissionId,
        CancellationToken cancellationToken = default)
        => await context.SubmissionReviews
            .AsNoTracking()
            .Where(review => review.GateType == gateType && review.SubmissionId == submissionId)
            .OrderBy(review => review.VersionNo)
            .ThenBy(review => review.ReviewedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(SubmissionReview review)
    {
        ArgumentNullException.ThrowIfNull(review);
        context.SubmissionReviews.Add(review);
    }
}

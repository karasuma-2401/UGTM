using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SubmissionProductLinkRepository(ThesisManagementDbContext context) : ISubmissionProductLinkRepository
{
    public async Task<IReadOnlyList<SubmissionProductLink>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
        => await context.SubmissionProductLinks
            .AsNoTracking()
            .Where(link => link.EntityType == entityType && link.EntityId == entityId)
            .OrderBy(link => link.VersionNo)
            .ThenBy(link => link.DisplayOrder)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<SubmissionProductLink> links)
    {
        ArgumentNullException.ThrowIfNull(links);
        context.SubmissionProductLinks.AddRange(links);
    }
}

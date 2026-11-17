using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SubmissionAttachmentRepository(ThesisManagementDbContext context) : ISubmissionAttachmentRepository
{
    public Task<SubmissionAttachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.SubmissionAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(attachment => attachment.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SubmissionAttachment>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
        => await context.SubmissionAttachments
            .AsNoTracking()
            .Where(attachment => attachment.EntityType == entityType && attachment.EntityId == entityId)
            .OrderBy(attachment => attachment.VersionNo)
            .ThenBy(attachment => attachment.DisplayOrder)
            .ToListAsync(cancellationToken);

    public void AddRange(IEnumerable<SubmissionAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        context.SubmissionAttachments.AddRange(attachments);
    }
}

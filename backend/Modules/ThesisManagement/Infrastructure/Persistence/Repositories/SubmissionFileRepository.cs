using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class SubmissionFileRepository(ThesisManagementDbContext context) : ISubmissionFileRepository
{
    public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.SubmissionFiles
            .AsNoTracking()
            .SingleOrDefaultAsync(file => file.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
        => await context.SubmissionFiles
            .AsNoTracking()
            .Where(file => file.EntityType == entityType && file.EntityId == entityId)
            .OrderBy(file => file.VersionNo)
            .ThenBy(file => file.UploadedAt)
            .ToListAsync(cancellationToken);

    public void Add(SubmissionFile submissionFile)
    {
        ArgumentNullException.ThrowIfNull(submissionFile);
        context.SubmissionFiles.Add(submissionFile);
    }
}

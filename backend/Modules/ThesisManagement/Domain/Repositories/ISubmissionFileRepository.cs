using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface ISubmissionFileRepository
{
    Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    void Add(SubmissionFile submissionFile);
}

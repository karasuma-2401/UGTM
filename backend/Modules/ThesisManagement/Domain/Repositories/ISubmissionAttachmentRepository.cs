using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface ISubmissionAttachmentRepository
{
    Task<SubmissionAttachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubmissionAttachment>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<SubmissionAttachment> attachments);
}

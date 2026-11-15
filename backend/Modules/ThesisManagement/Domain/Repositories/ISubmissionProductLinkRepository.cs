using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface ISubmissionProductLinkRepository
{
    Task<IReadOnlyList<SubmissionProductLink>> GetByEntityAsync(
        SubmissionEntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default);

    void AddRange(IEnumerable<SubmissionProductLink> links);
}

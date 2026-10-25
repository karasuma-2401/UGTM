using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IRenameRequestRepository
{
    void Add(RenameRequest request);

    Task<RenameRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenameRequest>> GetByGroupIdAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveRequestForGroupAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenameRequest>> ListAsync(
        Guid? groupId,
        RenameRequestStatus? status,
        IReadOnlyCollection<Guid>? topicIdsFilter,
        CancellationToken cancellationToken = default);
}

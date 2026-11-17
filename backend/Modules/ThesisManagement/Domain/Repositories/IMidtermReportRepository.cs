using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Domain.Repositories;

public interface IMidtermReportRepository
{
    Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);

    void Add(MidtermReport midtermReport);
}

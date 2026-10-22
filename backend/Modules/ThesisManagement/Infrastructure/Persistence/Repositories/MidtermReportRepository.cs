using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class MidtermReportRepository(ThesisManagementDbContext context) : IMidtermReportRepository
{
    public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.MidtermReports.SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

    public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => context.MidtermReports.SingleOrDefaultAsync(report => report.GroupId == groupId, cancellationToken);

    public void Add(MidtermReport midtermReport)
    {
        ArgumentNullException.ThrowIfNull(midtermReport);
        context.MidtermReports.Add(midtermReport);
    }
}

using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class ProgressReportRepository(ThesisManagementDbContext context) : IProgressReportRepository
{
    public Task<ProgressReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.ProgressReports.SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

    public Task<ProgressReport?> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default)
        => context.ProgressReports.SingleOrDefaultAsync(report => report.MilestoneId == milestoneId, cancellationToken);

    public async Task<IReadOnlyList<ProgressReportVersion>> GetVersionsAsync(Guid progressReportId, CancellationToken cancellationToken = default)
        => await context.ProgressReportVersions.AsNoTracking()
            .Where(version => version.ProgressReportId == progressReportId)
            .OrderBy(version => version.VersionNo)
            .ToListAsync(cancellationToken);

    public Task<ProgressReportVersion?> GetVersionAsync(Guid progressReportId, int versionNo, CancellationToken cancellationToken = default)
        => context.ProgressReportVersions.SingleOrDefaultAsync(version => version.ProgressReportId == progressReportId && version.VersionNo == versionNo, cancellationToken);

    public void Add(ProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        context.ProgressReports.Add(report);
    }

    public void AddVersion(ProgressReportVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        context.ProgressReportVersions.Add(version);
    }
}

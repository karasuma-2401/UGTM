using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class FinalReportRepository(ThesisManagementDbContext context) : IFinalReportRepository
{
    public Task<FinalReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.FinalReports.SingleOrDefaultAsync(report => report.Id == id, cancellationToken);

    public Task<FinalReport?> GetByGroupAndVersionTypeAsync(
        Guid groupId,
        FinalReportVersionType versionType,
        CancellationToken cancellationToken = default)
        => context.FinalReports.SingleOrDefaultAsync(
            report => report.GroupId == groupId && report.VersionType == versionType,
            cancellationToken);

    public void Add(FinalReport finalReport)
    {
        ArgumentNullException.ThrowIfNull(finalReport);
        context.FinalReports.Add(finalReport);
    }

    public void UpdateForResubmission(FinalReport finalReport, StudentGroup group)
    {
        ArgumentNullException.ThrowIfNull(finalReport);
        ArgumentNullException.ThrowIfNull(group);
        if (finalReport.GroupId != group.Id)
        {
            throw new ArgumentException("The report must belong to the group.", nameof(group));
        }

        // A revision leaves the group's status unchanged. Force its UPDATE so xmin also rejects
        // a group stopped or otherwise changed after eligibility was checked, in the same transaction.
        context.Entry(group).Property(value => value.Status).IsModified = true;
    }
}

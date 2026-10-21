using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;

namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Repositories;

public sealed class ScheduleTemplateRepository(AcademicPeriodsDbContext db) : IScheduleTemplateRepository
{
    public Task<ScheduleTemplateVersion?> GetActiveAsync(string templateCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = templateCode.Trim();
        return db.ScheduleTemplateVersions
            .Include(x => x.StageDefinitions)
            .SingleOrDefaultAsync(
                x => x.TemplateCode == normalizedCode && x.Status == ConfigurationVersionStatus.Active,
                cancellationToken);
    }

    public void Add(ScheduleTemplateVersion template) => db.ScheduleTemplateVersions.Add(template);
}

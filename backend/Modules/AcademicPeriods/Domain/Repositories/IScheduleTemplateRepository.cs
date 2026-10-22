using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Domain.Repositories;

public interface IScheduleTemplateRepository
{
    Task<ScheduleTemplateVersion?> GetActiveAsync(string templateCode, CancellationToken cancellationToken = default);
    void Add(ScheduleTemplateVersion template);
}

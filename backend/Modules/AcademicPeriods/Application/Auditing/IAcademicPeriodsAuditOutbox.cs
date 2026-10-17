using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.AcademicPeriods.Application.Auditing;

public interface IAcademicPeriodsAuditOutbox
{
    void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc);
}

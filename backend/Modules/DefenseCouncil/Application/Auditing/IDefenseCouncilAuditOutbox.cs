using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.DefenseCouncil.Application.Auditing;

public interface IDefenseCouncilAuditOutbox
{
    void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc);
}

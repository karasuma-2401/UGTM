using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.ThesisManagement.Application.Auditing;

public interface IThesisManagementAuditOutbox
{
    void Add(AuditIntegrationEntry entry, DateTime occurredAtUtc);
}

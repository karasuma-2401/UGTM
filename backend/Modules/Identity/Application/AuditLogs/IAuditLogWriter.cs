namespace UGTM.Modules.Identity.Application.AuditLogs;

public interface IAuditLogWriter
{
    void Add(AuditEntry entry);
}
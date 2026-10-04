using UGTM.Persistence.Entities.Identity;

namespace UGTM.Persistence.Entities.Shared;

public class AuditLog
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? OldData { get; set; }
    public string? NewData { get; set; }
    public DateTime Timestamp { get; set; }

    public User User { get; set; } = null!;
}

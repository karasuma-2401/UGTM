namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class TopicRegistrationEmail
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string Type { get; set; } = null!;
    public string ProtectionPurpose { get; set; } = null!;
    public string ProtectedPayload { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastErrorCode { get; set; }
}

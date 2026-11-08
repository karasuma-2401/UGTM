namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class ThesisUploadAttempt
{
    public Guid Id { get; set; }
    public string ObjectKey { get; set; } = null!;
    public string State { get; set; } = "Uploading";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LeaseExpiresAtUtc { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastErrorCode { get; set; }
}

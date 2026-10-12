using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.Identity.Domain.Entities;

public sealed class IdentityOutboxMessage : Entity<Guid>
{
    private IdentityOutboxMessage() { }

    private IdentityOutboxMessage(
        Guid id,
        string type,
        string protectedPayload,
        string protectionPurpose,
        int schemaVersion,
        DateTime createdAt)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectionPurpose);
        
        Type  = type;
        ProtectedPayload = protectedPayload;
        ProtectionPurpose = protectionPurpose;
        SchemaVersion = schemaVersion;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }
    public string Type { get; private set; } = null!;
    public string ProtectedPayload { get; private set; } = null!;
    public string ProtectionPurpose { get; private set; } = null!;
    public int SchemaVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    
    public static IdentityOutboxMessage Create(
        string type,
        string protectedPayload,
        string protectionPurpose,
        DateTime createdAt)
        => new(
            Guid.NewGuid(),
            type,
            protectedPayload,
            protectionPurpose,
            1,
            createdAt);

    public void MarkProcessed(DateTime now)
    {
        ProcessedAt = now;
        ProtectedPayload = string.Empty;
        LastError = null;
    }

    // make delay time increase exponential
    public void MarkFailed(string error, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        AttemptCount++;
        LastError = error.Length <= 1000 ? error : error[..1000];
        var delayMinutes = Math.Min(Math.Pow(2, AttemptCount), 60);
        NextAttemptAt = now.AddMinutes(delayMinutes);
    }
    
}
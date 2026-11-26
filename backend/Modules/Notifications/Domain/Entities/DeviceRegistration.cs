namespace UGTM.Modules.Notifications.Domain.Entities;

public sealed class DeviceRegistration
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string InstallationId { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string Platform { get; set; } = "web";
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null;

    public void ReassignOrRefresh(
        Guid userId,
        string installationId,
        string protectedToken,
        string platform,
        string? userAgent,
        DateTime nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        UserId = userId;
        InstallationId = installationId;
        Token = protectedToken;
        Platform = platform;
        UserAgent = userAgent;
        LastSeenAt = nowUtc;
        RevokedAt = null;
    }

    public void Revoke(DateTime nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        RevokedAt ??= nowUtc;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC.", parameterName);
        }
    }
}

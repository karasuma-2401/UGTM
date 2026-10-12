namespace UGTM.Modules.Identity.Application.Security;

public sealed class IdentitySecurityPolicy
{
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan SessionIdleLifetime { get; init; } = TimeSpan.FromDays(7);
    public TimeSpan SessionAbsoluteLifetime { get; init; } = TimeSpan.FromDays(30);
    public TimeSpan ActivationLifetime { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan RecoveryOtpLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan ResetGrantLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public int ChallengeMaxAttempts { get; init; } = 5;
}
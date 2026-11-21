namespace UGTM.Modules.Identity.Application.Users.ResetPassword;

public enum AdminPasswordResetMode
{
    Custom,
    Generated
}

public sealed record AdminResetPasswordCommand(
    Guid TargetUserId,
    string Mode,
    string? NewPassword,
    bool RequirePasswordChange,
    string AdminPassword,
    string? Reason);

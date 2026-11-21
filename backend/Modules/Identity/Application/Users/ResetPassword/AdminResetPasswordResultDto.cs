namespace UGTM.Modules.Identity.Application.Users.ResetPassword;

public sealed record AdminResetPasswordResultDto(
    Guid UserId,
    string Mode,
    string? TemporaryPassword,
    bool MustChangePassword,
    DateTime PasswordChangedAt,
    bool NotificationEmailQueued);

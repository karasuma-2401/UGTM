namespace UGTM.Modules.Identity.Application.AuditLogs;

public static class AuditActions
{
    public const string UserCreated = "Identity.User.Created";
    public const string UserUpdated = "Identity.User.Updated";
    public const string UserStatusChanged = "Identity.User.StatusChanged";
    public const string UserRolesChanged = "Identity.User.RolesChanged";
    public const string UsersImported = "Identity.Users.Imported";
    public const string RoleCreated = "Identity.Role.Created";
    public const string RolePermissionsChanged = "Identity.Role.PermissionsChanged";
    public const string SessionRevoked = "Identity.Session.Revoked";
    public const string UserSessionsRevoked = "Identity.User.SessionsRevoked";
    public const string Login = "Identity.Authentication.Login";
    public const string SessionRefreshed = "Identity.Session.Refreshed";
    public const string RefreshTokenReuseDetected = "Identity.Session.RefreshTokenReuseDetected";
    public const string SessionLoggedOut = "Identity.Session.LoggedOut";
    public const string AccountActivated = "Identity.Account.Activated";
    public const string ActivationRequested = "Identity.Account.ActivationRequested";
    public const string PasswordChanged = "Identity.Account.PasswordChanged";
    public const string PasswordRecoveryRequested = "Identity.Account.PasswordRecoveryRequested";
    public const string RecoveryOtpVerified = "Identity.Account.RecoveryOtpVerified";
    public const string PasswordReset = "Identity.Account.PasswordReset";
}

public static class AuditOutcomes
{
    public const string Success = "Success";
    public const string Denied = "Denied";
    public const string Failed = "Failed";
}

namespace UGTM.Modules.Identity.Domain.Enums;

public enum SessionRevocationReason
{
    UserLogout,
    UserLogoutAll,
    PasswordChanged,
    PasswordReset,
    RefreshTokenReuse,
    AccountDisabled,
    AdministratorRevoked
}
namespace UGTM.Modules.Identity.Application.Authentication.ChangePassword;

public sealed record ChangePasswordCommand( 
    string CurrentPassword,
    string NewPassword);
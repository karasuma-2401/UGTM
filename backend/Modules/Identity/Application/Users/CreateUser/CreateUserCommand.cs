namespace UGTM.Modules.Identity.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string? UserCode,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    string? InitialPassword = null,
    bool SendActivationEmail = true);

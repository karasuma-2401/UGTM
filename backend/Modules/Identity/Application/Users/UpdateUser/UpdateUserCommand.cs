namespace UGTM.Modules.Identity.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(
    Guid Id,
    string FullName,
    string? UserCode,
    string? PhoneNumber,
    IReadOnlyList<string>? Roles);

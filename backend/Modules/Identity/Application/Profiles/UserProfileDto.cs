namespace UGTM.Modules.Identity.Application.Profiles;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string? UserCode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
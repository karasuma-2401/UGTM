namespace UGTM.Modules.Identity.Application.Profiles;

public sealed record UserPermissionDetailDto(
    string PermissionCode,
    string? PermissionName,
    string? Module);

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    string? UserCode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword = false,
    string? PhoneNumber = null,
    string Status = "Active",
    DateTime? CreatedAt = null,
    DateTime? ActivatedAt = null,
    DateTime? LastLoginAt = null,
    DateTime? PasswordChangedAt = null,
    IReadOnlyList<UserPermissionDetailDto>? PermissionDetails = null);

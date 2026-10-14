namespace UGTM.Modules.Identity.Application.Users.Dtos;

public sealed record UserSummaryDto(
    Guid Id,
    string Email,
    string FullName,
    string? UserCode,
    string? PhoneNumber,
    string Status,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

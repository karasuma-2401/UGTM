namespace UGTM.Modules.Identity.Application.Users.GetUsers;

public sealed record GetUsersQuery(
    string? Keyword = null,
    string? Role = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 10,
    string? AccountGroup = null);

public enum UserAccountGroup
{
    Student,
    Staff
}

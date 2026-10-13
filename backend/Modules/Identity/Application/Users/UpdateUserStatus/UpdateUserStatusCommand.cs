namespace UGTM.Modules.Identity.Application.Users.UpdateUserStatus;

public sealed record UpdateUserStatusCommand(
    Guid Id,
    string Status);

namespace UGTM.Modules.Identity.Application.Users.ImportUsers;

public sealed record ImportUsersCommand(
    Stream File,
    string FileName,
    string Role);

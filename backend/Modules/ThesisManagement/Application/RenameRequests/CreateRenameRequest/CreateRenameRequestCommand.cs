namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;

public sealed record CreateRenameRequestCommand(
    Guid GroupId,
    string NewTitleVi,
    string? NewTitleEn,
    string Reason);

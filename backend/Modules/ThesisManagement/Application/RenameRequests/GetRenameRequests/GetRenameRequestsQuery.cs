namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.GetRenameRequests;

public sealed record GetRenameRequestsQuery(
    Guid? GroupId,
    string? Status);

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;

public sealed record ReviewRenameRequestByFacultyCommand(
    Guid RequestId,
    string Decision,
    string? RejectionReason);

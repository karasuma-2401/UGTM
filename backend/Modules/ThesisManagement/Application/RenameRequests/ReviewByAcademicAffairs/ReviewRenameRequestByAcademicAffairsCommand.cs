namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;

public sealed record ReviewRenameRequestByAcademicAffairsCommand(
    Guid RequestId,
    string Decision,
    string? RejectionReason);

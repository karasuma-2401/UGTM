namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;

public sealed record ReviewRenameRequestByAdvisorCommand(
    Guid RequestId,
    string Decision,
    string? RejectionReason);

using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;

public sealed record CreateRenameRequestInputDto(
    string NewTitleVi,
    string? NewTitleEn,
    string Reason);

public sealed record ReviewRenameRequestInputDto(
    string Decision,
    string? RejectionReason);

public sealed record RenameRequestDto(
    Guid Id,
    Guid GroupId,
    Guid RequestedByStudentId,
    string OldTitleVi,
    string NewTitleVi,
    string? OldTitleEn,
    string? NewTitleEn,
    string Reason,
    string Status,
    string? RejectionReason,
    Guid? RejectedBy,
    DateTime? RejectedAt,
    DateTime? AdvisorConfirmedAt,
    DateTime? FacultyApprovedAt,
    DateTime? AaApprovedAt,
    DateTime CreatedAt);

public static class RenameRequestMapper
{
    public static RenameRequestDto ToDto(RenameRequest r) => new(
        r.Id,
        r.GroupId,
        r.RequestedByStudentId,
        r.OldTitleVi,
        r.NewTitleVi,
        r.OldTitleEn,
        r.NewTitleEn,
        r.Reason,
        r.Status.ToString(),
        r.RejectionReason,
        r.RejectedBy,
        r.RejectedAt,
        r.AdvisorConfirmedAt,
        r.FacultyApprovedAt,
        r.AaApprovedAt,
        r.CreatedAt);
}

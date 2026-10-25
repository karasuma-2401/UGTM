using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class RenameRequest
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid RequestedByStudentId { get; set; }
    public string OldTitleVi { get; set; } = null!;
    public string NewTitleVi { get; set; } = null!;
    public string? OldTitleEn { get; set; }
    public string? NewTitleEn { get; set; }
    public string Reason { get; set; } = null!;
    public RenameRequestStatus Status { get; set; } = RenameRequestStatus.Created;
    public string? RejectionReason { get; set; }
    public Guid? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? AdvisorConfirmedAt { get; set; }
    public DateTime? FacultyApprovedAt { get; set; }
    public DateTime? AaApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public StudentGroup Group { get; set; } = null!;

    public static RenameRequest Create(
        Guid id,
        Guid groupId,
        Guid requestedByStudentId,
        string oldTitleVi,
        string newTitleVi,
        string? oldTitleEn,
        string? newTitleEn,
        string reason,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || groupId == Guid.Empty || requestedByStudentId == Guid.Empty)
        {
            throw new ArgumentException("Request, group and student identifiers must be nonempty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(oldTitleVi);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTitleVi);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var trimmedOldVi = oldTitleVi.Trim();
        var trimmedNewVi = newTitleVi.Trim();
        var trimmedOldEn = string.IsNullOrWhiteSpace(oldTitleEn) ? null : oldTitleEn.Trim();
        var trimmedNewEn = string.IsNullOrWhiteSpace(newTitleEn) ? null : newTitleEn.Trim();
        var trimmedReason = reason.Trim();

        if (trimmedNewVi.Length > 300)
        {
            throw new ArgumentException("New Vietnamese title cannot exceed 300 characters.", nameof(newTitleVi));
        }

        if (trimmedNewEn is not null && trimmedNewEn.Length > 350)
        {
            throw new ArgumentException("New English title cannot exceed 350 characters.", nameof(newTitleEn));
        }

        var isViSame = string.Equals(trimmedOldVi, trimmedNewVi, StringComparison.OrdinalIgnoreCase);
        var isEnSame = string.Equals(trimmedOldEn ?? string.Empty, trimmedNewEn ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (isViSame && isEnSame)
        {
            throw new InvalidOperationException("New title must be different from current title.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Creation time must be UTC.", nameof(createdAtUtc));
        }

        return new RenameRequest
        {
            Id = id,
            GroupId = groupId,
            RequestedByStudentId = requestedByStudentId,
            OldTitleVi = trimmedOldVi,
            NewTitleVi = trimmedNewVi,
            OldTitleEn = trimmedOldEn,
            NewTitleEn = trimmedNewEn,
            Reason = trimmedReason,
            Status = RenameRequestStatus.Created,
            CreatedAt = createdAtUtc
        };
    }

    public void ApproveByAdvisor(DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.Created)
        {
            throw new InvalidOperationException("Only newly created requests can be approved by the advisor.");
        }

        Status = RenameRequestStatus.AdvisorConfirmed;
        AdvisorConfirmedAt = EnsureUtc(occurredAtUtc);
    }

    public void RejectByAdvisor(string rejectionReason, Guid rejectedBy, DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.Created)
        {
            throw new InvalidOperationException("Only newly created requests can be rejected by the advisor.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rejectionReason);
        if (rejectedBy == Guid.Empty)
        {
            throw new ArgumentException("Reviewer identifier must be nonempty.", nameof(rejectedBy));
        }

        Status = RenameRequestStatus.RejectedByAdvisor;
        RejectionReason = rejectionReason.Trim();
        RejectedBy = rejectedBy;
        RejectedAt = EnsureUtc(occurredAtUtc);
    }

    public void ApproveByFaculty(DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.AdvisorConfirmed)
        {
            throw new InvalidOperationException("Only advisor-confirmed requests can be approved by the faculty.");
        }

        Status = RenameRequestStatus.FacultyApproved;
        FacultyApprovedAt = EnsureUtc(occurredAtUtc);
    }

    public void RejectByFaculty(string rejectionReason, Guid rejectedBy, DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.AdvisorConfirmed)
        {
            throw new InvalidOperationException("Only advisor-confirmed requests can be rejected by the faculty.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rejectionReason);
        if (rejectedBy == Guid.Empty)
        {
            throw new ArgumentException("Reviewer identifier must be nonempty.", nameof(rejectedBy));
        }

        Status = RenameRequestStatus.RejectedByFaculty;
        RejectionReason = rejectionReason.Trim();
        RejectedBy = rejectedBy;
        RejectedAt = EnsureUtc(occurredAtUtc);
    }

    public void ApproveByAcademicAffairs(DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.FacultyApproved)
        {
            throw new InvalidOperationException("Only faculty-approved requests can be officially approved by academic affairs.");
        }

        Status = RenameRequestStatus.Completed;
        AaApprovedAt = EnsureUtc(occurredAtUtc);
    }

    public void RejectByAcademicAffairs(string rejectionReason, Guid rejectedBy, DateTime occurredAtUtc)
    {
        if (Status != RenameRequestStatus.FacultyApproved)
        {
            throw new InvalidOperationException("Only faculty-approved requests can be rejected by academic affairs.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rejectionReason);
        if (rejectedBy == Guid.Empty)
        {
            throw new ArgumentException("Reviewer identifier must be nonempty.", nameof(rejectedBy));
        }

        Status = RenameRequestStatus.RejectedByAA;
        RejectionReason = rejectionReason.Trim();
        RejectedBy = rejectedBy;
        RejectedAt = EnsureUtc(occurredAtUtc);
    }

    private static DateTime EnsureUtc(DateTime dt)
    {
        if (dt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Operation time must be in UTC.");
        }
        return dt;
    }
}

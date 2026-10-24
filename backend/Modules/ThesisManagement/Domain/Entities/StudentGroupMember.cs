using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class StudentGroupMember
{
    private StudentGroupMember() { }

    internal StudentGroupMember(
        StudentGroup group,
        Guid studentId,
        Guid batchId,
        DateTime joinedAt,
        Guid? invitedByStudentId = null,
        MemberConfirmationStatus confirmationStatus = MemberConfirmationStatus.Confirmed,
        string? confirmationTokenHash = null,
        DateTime? confirmationExpiresAtUtc = null,
        DateTime? confirmedAtUtc = null)
    {
        Id = Guid.NewGuid();
        Group = group;
        GroupId = group.Id;
        StudentId = studentId;
        BatchId = batchId;
        JoinedAt = joinedAt;
        InvitedByStudentId = invitedByStudentId;
        ConfirmationStatus = confirmationStatus;
        ConfirmationTokenHash = confirmationTokenHash;
        ConfirmationExpiresAtUtc = confirmationExpiresAtUtc;
        ConfirmedAtUtc = confirmedAtUtc ?? (confirmationStatus == MemberConfirmationStatus.Confirmed ? joinedAt : null);
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid BatchId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public Guid? InvitedByStudentId { get; private set; }
    public MemberConfirmationStatus ConfirmationStatus { get; private set; }
    public string? ConfirmationTokenHash { get; private set; }
    public DateTime? ConfirmationExpiresAtUtc { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }

    public StudentGroup Group { get; private set; } = null!;

    public bool IsExpired(DateTime nowUtc)
        => ConfirmationStatus == MemberConfirmationStatus.Pending
           && ConfirmationExpiresAtUtc.HasValue
           && ConfirmationExpiresAtUtc.Value <= nowUtc;

    public void Confirm(DateTime confirmedAtUtc)
    {
        if (ConfirmationStatus == MemberConfirmationStatus.Confirmed)
        {
            return;
        }

        if (IsExpired(confirmedAtUtc))
        {
            throw new InvalidOperationException("Confirmation period has expired.");
        }

        ConfirmationStatus = MemberConfirmationStatus.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc;
    }
}

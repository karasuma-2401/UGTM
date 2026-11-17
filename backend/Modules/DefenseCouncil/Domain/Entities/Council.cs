using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Domain.Entities;

public class Council
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public string CouncilCode { get; set; } = null!;
    public DateOnly DefenseDate { get; set; }
    public string TimeSlot { get; set; } = null!;
    public string Room { get; set; } = null!;
    public CouncilStatus Status { get; set; } = CouncilStatus.Draft;
    public string? DecisionNumber { get; set; }
    public DateOnly? SignedDate { get; set; }
    public string? DecisionFileUrl { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsSubmittedToAcademicAffairs { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public string? SubmissionNotes { get; set; }

    public ICollection<CouncilMember> Members { get; set; } = [];
    public ICollection<CouncilAssignment> Assignments { get; set; } = [];

    public static Council Create(
        Guid batchId,
        string councilCode,
        DateOnly defenseDate,
        string timeSlot,
        string room)
    {
        return new Council
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            CouncilCode = councilCode.Trim(),
            DefenseDate = defenseDate,
            TimeSlot = timeSlot.Trim(),
            Room = room.Trim(),
            Status = CouncilStatus.Draft
        };
    }

    public void Approve(
        string decisionNumber,
        DateOnly? signedDate,
        string? decisionFileUrl,
        Guid approvedBy,
        DateTime approvedAt)
    {
        Status = CouncilStatus.Active;
        DecisionNumber = decisionNumber.Trim();
        SignedDate = signedDate;
        DecisionFileUrl = string.IsNullOrWhiteSpace(decisionFileUrl) ? null : decisionFileUrl.Trim();
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
    }

    public void UpdateSchedule(DateOnly defenseDate, string timeSlot, string room)
    {
        DefenseDate = defenseDate;
        TimeSlot = timeSlot.Trim();
        Room = room.Trim();
        IsSubmittedToAcademicAffairs = false;
        SubmittedAt = null;
        SubmittedBy = null;
        SubmissionNotes = null;
    }

    public void SubmitToAcademicAffairs(Guid actorId, string? notes, DateTime atUtc)
    {
        IsSubmittedToAcademicAffairs = true;
        SubmittedBy = actorId;
        SubmittedAt = atUtc;
        SubmissionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

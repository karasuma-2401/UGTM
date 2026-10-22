using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class ReviewGateConfiguration
{
    private ReviewGateConfiguration()
    {
    }

    public ReviewGateConfiguration(
        Guid id,
        Guid batchId,
        ReviewGateType gateType,
        DateTime initialSubmissionDeadlineUtc,
        DateTime facultyResponseDeadlineUtc,
        DateTime finalClosureDeadlineUtc,
        Guid createdBy,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || batchId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new ArgumentException("Configuration, batch, and creator identifiers must be nonempty.");
        }

        ValidateDeadlines(
            initialSubmissionDeadlineUtc,
            facultyResponseDeadlineUtc,
            finalClosureDeadlineUtc);
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = id;
        BatchId = batchId;
        GateType = gateType;
        InitialSubmissionDeadlineUtc = initialSubmissionDeadlineUtc;
        FacultyResponseDeadlineUtc = facultyResponseDeadlineUtc;
        FinalClosureDeadlineUtc = finalClosureDeadlineUtc;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        UpdatedBy = createdBy;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BatchId { get; private set; }
    public ReviewGateType GateType { get; private set; }
    public DateTime InitialSubmissionDeadlineUtc { get; private set; }
    public DateTime FacultyResponseDeadlineUtc { get; private set; }
    public DateTime FinalClosureDeadlineUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateDeadlines(
        DateTime initialSubmissionDeadlineUtc,
        DateTime facultyResponseDeadlineUtc,
        DateTime finalClosureDeadlineUtc,
        Guid updatedBy,
        DateTime updatedAtUtc)
    {
        if (updatedBy == Guid.Empty)
        {
            throw new ArgumentException("Updater identifier must be nonempty.", nameof(updatedBy));
        }

        ValidateDeadlines(
            initialSubmissionDeadlineUtc,
            facultyResponseDeadlineUtc,
            finalClosureDeadlineUtc);
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException("Update time cannot precede creation time.", nameof(updatedAtUtc));
        }

        InitialSubmissionDeadlineUtc = initialSubmissionDeadlineUtc;
        FacultyResponseDeadlineUtc = facultyResponseDeadlineUtc;
        FinalClosureDeadlineUtc = finalClosureDeadlineUtc;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidateDeadlines(
        DateTime initialSubmissionDeadlineUtc,
        DateTime facultyResponseDeadlineUtc,
        DateTime finalClosureDeadlineUtc)
    {
        EnsureUtc(initialSubmissionDeadlineUtc, nameof(initialSubmissionDeadlineUtc));
        EnsureUtc(facultyResponseDeadlineUtc, nameof(facultyResponseDeadlineUtc));
        EnsureUtc(finalClosureDeadlineUtc, nameof(finalClosureDeadlineUtc));

        if (initialSubmissionDeadlineUtc >= facultyResponseDeadlineUtc
            || facultyResponseDeadlineUtc >= finalClosureDeadlineUtc)
        {
            throw new ArgumentException(
                "Initial submission, faculty response, and final closure deadlines must be strictly increasing.");
        }
    }

    private static void EnsureUtc(DateTime timestamp, string parameterName)
    {
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }
    }
}

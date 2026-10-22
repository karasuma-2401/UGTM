
namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class ProgressMilestone
{
    private ProgressMilestone() { }

    private ProgressMilestone(
        Guid id,
        Guid groupId,
        string title,
        string? description,
        DateTime deadline,
        Guid createdBy,
        DateTime createdAt)
    {
        if (id == Guid.Empty || groupId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new ArgumentException("Milestone, group, and creator identifiers must be nonempty.");
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250)
        {
            throw new ArgumentException("Milestone title is required and cannot exceed 250 characters.", nameof(title));
        }

        if (deadline.Kind != DateTimeKind.Utc || createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Milestone dates must be UTC.");
        }

        if (deadline <= createdAt)
        {
            throw new ArgumentException("Milestone deadline must be later than its creation time.", nameof(deadline));
        }

        Id = id;
        GroupId = groupId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Deadline = deadline;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime Deadline { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public StudentGroup Group { get; private set; } = null!;
    public ICollection<ProgressReport> ProgressReports { get; private set; } = [];

    public static ProgressMilestone Create(
        Guid id,
        Guid groupId,
        string title,
        string? description,
        DateTime deadline,
        Guid createdBy,
        DateTime createdAt)
        => new(id, groupId, title, description, deadline, createdBy, createdAt);
}

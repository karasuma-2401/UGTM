using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class SubmissionProductLink
{
    public Guid Id { get; set; }
    public SubmissionEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public int VersionNo { get; set; } = 1;
    public int DisplayOrder { get; set; }
    public SubmissionLinkCategory Category { get; set; } = SubmissionLinkCategory.Other;
    public string? Label { get; set; }
    public string Url { get; set; } = null!;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

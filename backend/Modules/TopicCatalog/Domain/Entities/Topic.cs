using UGTM.Modules.TopicCatalog.Domain.Enums;

namespace UGTM.Modules.TopicCatalog.Domain.Entities;

public class Topic
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid AdvisorId { get; set; }
    public string TitleVi { get; set; } = null!;
    public string? TitleEn { get; set; }
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public TopicStatus Status { get; set; } = TopicStatus.Draft;
    public int? StudentNumber { get; set; }
    public DateTime CreatedAt { get; set; }

}

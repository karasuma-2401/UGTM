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

    public bool IsPublished => Status == TopicStatus.Published;

    public bool CanBeModified => Status is TopicStatus.Draft or TopicStatus.NeedRevision;

    public void UpdateDetails(
        string titleVi,
        string? titleEn,
        string? description,
        string? requirements,
        int studentNumber)
    {
        TitleVi = titleVi;
        TitleEn = titleEn;
        Description = description;
        Requirements = requirements;
        StudentNumber = studentNumber;
    }

    public bool TrySubmit()
    {
        if (Status is not (TopicStatus.Draft or TopicStatus.NeedRevision))
        {
            return false;
        }

        Status = TopicStatus.Submitted;
        return true;
    }

    public bool TryReview(TopicStatus decision)
    {
        if (Status != TopicStatus.Submitted)
        {
            return false;
        }

        if (decision is not (TopicStatus.Approved or TopicStatus.NeedRevision or TopicStatus.Rejected))
        {
            return false;
        }

        Status = decision;
        return true;
    }

    public bool TryPublish()
    {
        if (Status != TopicStatus.Approved)
        {
            return false;
        }

        Status = TopicStatus.Published;
        return true;
    }
}

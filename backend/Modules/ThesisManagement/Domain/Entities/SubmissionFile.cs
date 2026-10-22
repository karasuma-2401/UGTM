using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public class SubmissionFile
{
    public Guid Id { get; set; }
    public SubmissionEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public int VersionNo { get; set; } = 1;
    public string FileName { get; set; } = null!;
    public string ObjectKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string? ChecksumSha256 { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; } = null!;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; }

}

using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class SubmissionEvidenceHelper
{
    public sealed record PreparedAttachment(
        SubmissionAttachment Entity,
        ReadOnlyMemory<byte> Content);

    public static async Task<IReadOnlyList<PreparedAttachment>> PrepareImagesAsync(
        IReadOnlyList<SubmissionAttachmentUpload>? uploads,
        IThesisSubmissionImageValidator? imageValidator,
        IThesisSubmissionObjectKeyFactory objectKeyFactory,
        SubmissionEntityType entityType,
        Guid entityId,
        int versionNo,
        Guid uploadedBy,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        if (uploads is null || uploads.Count == 0)
        {
            return [];
        }

        if (uploads.Count > ThesisSubmissionImageValidator.MaximumEvidenceImageCount)
        {
            throw new ThesisSubmissionFileValidationException(
                $"Chỉ được đính kèm tối đa {ThesisSubmissionImageValidator.MaximumEvidenceImageCount} ảnh minh chứng cho mỗi lần nộp.");
        }

        var validator = imageValidator ?? new ThesisSubmissionImageValidator();
        var prepared = new List<PreparedAttachment>(uploads.Count);

        for (var i = 0; i < uploads.Count; i++)
        {
            var upload = uploads[i];
            if (string.IsNullOrWhiteSpace(upload.FileName) || upload.Content is null || !upload.Content.CanRead)
            {
                throw new ThesisSubmissionFileValidationException("Ảnh minh chứng không hợp lệ.");
            }

            var validated = await validator.ValidateAsync(upload.FileName, upload.Content, cancellationToken);
            var objectKey = objectKeyFactory.CreateAttachment(entityType, entityId, versionNo, validated.FileExtension);

            var entity = new SubmissionAttachment
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                VersionNo = versionNo,
                DisplayOrder = i,
                FileName = validated.FileName,
                ObjectKey = objectKey,
                ContentType = validated.ContentType,
                ChecksumSha256 = validated.ChecksumSha256,
                FileSize = validated.FileSize,
                FileExtension = validated.FileExtension,
                UploadedBy = uploadedBy,
                UploadedAt = uploadedAt
            };

            prepared.Add(new PreparedAttachment(entity, validated.Content));
        }

        return prepared;
    }

    public static async Task UploadAllAsync(
        IReadOnlyList<PreparedAttachment> items,
        IThesisSubmissionObjectStorage objectStorage,
        ICollection<string> uploadedObjectKeys,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            await using var stream = new MemoryStream(item.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(
                new ThesisSubmissionObjectWriteRequest(
                    item.Entity.ObjectKey,
                    item.Entity.ContentType,
                    item.Entity.FileSize),
                stream,
                cancellationToken);
            uploadedObjectKeys.Add(item.Entity.ObjectKey);
        }
    }

    public static IReadOnlyList<SubmissionProductLink> BuildProductLinks(
        IReadOnlyList<SubmissionProductLinkValidator.NormalizedProductLink> normalizedLinks,
        SubmissionEntityType entityType,
        Guid entityId,
        int versionNo,
        Guid createdBy,
        DateTime createdAt)
        => normalizedLinks
            .Select((link, index) => new SubmissionProductLink
            {
                Id = Guid.NewGuid(),
                EntityType = entityType,
                EntityId = entityId,
                VersionNo = versionNo,
                DisplayOrder = index,
                Category = link.Category,
                Label = link.Label,
                Url = link.Url,
                CreatedBy = createdBy,
                CreatedAt = createdAt
            })
            .ToArray();
}

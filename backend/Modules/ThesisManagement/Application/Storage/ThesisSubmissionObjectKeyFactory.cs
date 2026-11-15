using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Storage;

public sealed class ThesisSubmissionObjectKeyFactory : IThesisSubmissionObjectKeyFactory
{
    public string Create(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension)
    {
        if (!Enum.IsDefined(entityType))
        {
            throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unsupported submission entity type.");
        }

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("The submission entity identifier is required.", nameof(entityId));
        }

        if (versionNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNo), "The submission version must be positive.");
        }

        var normalizedExtension = NormalizeExtension(fileExtension);

        return $"thesis-submissions/{entityType.ToString().ToLowerInvariant()}/{entityId:N}/v{versionNo}/{Guid.NewGuid():N}{normalizedExtension}";
    }

    public string CreateAttachment(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension)
    {
        if (!Enum.IsDefined(entityType))
        {
            throw new ArgumentOutOfRangeException(nameof(entityType), entityType, "Unsupported submission entity type.");
        }

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("The submission entity identifier is required.", nameof(entityId));
        }

        if (versionNo <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNo), "The submission version must be positive.");
        }

        var normalizedExtension = NormalizeImageExtension(fileExtension);

        return $"thesis-submissions/{entityType.ToString().ToLowerInvariant()}/evidence/{entityId:N}/v{versionNo}/{Guid.NewGuid():N}{normalizedExtension}";
    }

    private static string NormalizeExtension(string fileExtension)
    {
        var normalizedExtension = fileExtension?.Trim().ToLowerInvariant();

        if (normalizedExtension is not (".pdf" or ".doc" or ".docx"))
        {
            throw new ArgumentException("Only PDF, DOC and DOCX extensions are supported.", nameof(fileExtension));
        }

        return normalizedExtension;
    }

    private static string NormalizeImageExtension(string fileExtension)
    {
        var normalizedExtension = fileExtension?.Trim().ToLowerInvariant();

        if (normalizedExtension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            throw new ArgumentException("Only PNG, JPG, JPEG and WEBP extensions are supported.", nameof(fileExtension));
        }

        return normalizedExtension;
    }
}

using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Storage;

/// <summary>
/// Creates opaque, immutable keys for private thesis submission objects.
/// </summary>
public interface IThesisSubmissionObjectKeyFactory
{
    string Create(SubmissionEntityType entityType, Guid entityId, int versionNo, string fileExtension);
}

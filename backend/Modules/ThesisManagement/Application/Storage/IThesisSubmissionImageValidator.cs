namespace UGTM.Modules.ThesisManagement.Application.Storage;

/// <summary>
/// Validates evidence image attachments (PNG, JPG, JPEG, WEBP) before persisting them to private object storage.
/// </summary>
public interface IThesisSubmissionImageValidator
{
    Task<ValidatedThesisSubmissionFile> ValidateAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

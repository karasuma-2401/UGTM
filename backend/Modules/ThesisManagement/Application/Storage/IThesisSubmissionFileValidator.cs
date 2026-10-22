namespace UGTM.Modules.ThesisManagement.Application.Storage;

/// <summary>
/// Validates a file submitted for a thesis workflow before it is persisted in object storage.
/// </summary>
public interface IThesisSubmissionFileValidator
{
    Task<ValidatedThesisSubmissionFile> ValidateAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

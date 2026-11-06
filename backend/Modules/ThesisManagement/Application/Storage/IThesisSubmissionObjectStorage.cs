namespace UGTM.Modules.ThesisManagement.Application.Storage;

public sealed record ThesisSubmissionObjectWriteRequest(
    string ObjectKey,
    string ContentType,
    long FileSize);

/// <summary>
/// Stores thesis submission files in a private object store.
/// Callers must authorize a user against the owning submission before requesting a download URI.
/// If the database transaction that references an uploaded object fails, callers must compensate by deleting it.
/// </summary>
public interface IThesisSubmissionObjectStorage
{
    Task UploadAsync(
        ThesisSubmissionObjectWriteRequest request,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Uri> CreateDownloadUriAsync(
        string objectKey,
        TimeSpan validFor,
        CancellationToken cancellationToken = default);

    /// <summary>Signs an attachment response with the validated original file name.</summary>
    Task<Uri> CreateDownloadUriAsync(string objectKey, string downloadFileName, TimeSpan validFor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The storage provider does not support named attachment downloads.");

    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The storage provider does not support opening streams directly.");

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}

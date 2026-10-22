namespace UGTM.Modules.ThesisManagement.Application.Storage;

/// <summary>
/// A validated, in-memory submission file. The content is bounded to 20 MiB by the validator.
/// </summary>
public sealed record ValidatedThesisSubmissionFile(
    string FileName,
    string FileExtension,
    string ContentType,
    long FileSize,
    string ChecksumSha256,
    ReadOnlyMemory<byte> Content);

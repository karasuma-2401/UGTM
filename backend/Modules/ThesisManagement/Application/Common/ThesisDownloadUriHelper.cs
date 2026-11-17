using UGTM.Modules.ThesisManagement.Application.Storage;

namespace UGTM.Modules.ThesisManagement.Application.Common;

internal static class ThesisDownloadUriHelper
{
    private const int MaxFileNameLength = 255;

    internal static Task<Uri> CreateSignedUriAsync(
        IThesisSubmissionObjectStorage storage,
        string objectKey,
        string? rawFileName,
        string? fallbackExtension,
        TimeSpan validFor,
        bool inline,
        CancellationToken cancellationToken)
    {
        var safeFileName = NormalizeDownloadFileName(rawFileName, fallbackExtension);
        return inline
            ? storage.CreateDownloadUriAsync(objectKey, safeFileName, validFor, true, cancellationToken)
            : storage.CreateDownloadUriAsync(objectKey, safeFileName, validFor, cancellationToken);
    }

    internal static string NormalizeDownloadFileName(string? rawFileName, string? fallbackExtension)
    {
        var leaf = (rawFileName ?? string.Empty)
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? string.Empty;
        var cleaned = new string(leaf.Trim().Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            var ext = NormalizeExtension(fallbackExtension);
            return $"download{ext}";
        }

        if (cleaned.Length <= MaxFileNameLength)
        {
            return cleaned;
        }

        var preservedExt = Path.GetExtension(cleaned);
        if (preservedExt.Length is > 0 and < 32)
        {
            var stemMaxLength = MaxFileNameLength - preservedExt.Length;
            return $"{cleaned[..stemMaxLength]}{preservedExt}";
        }

        return cleaned[..MaxFileNameLength];
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return string.Empty;
        var trimmed = new string(extension.Trim().Where(c => !char.IsControl(c) && c is not ('/' or '\\')).ToArray());
        if (string.IsNullOrEmpty(trimmed)) return string.Empty;
        return trimmed.StartsWith('.') ? trimmed : $".{trimmed}";
    }
}

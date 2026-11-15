using System.Buffers;
using System.Security.Cryptography;

namespace UGTM.Modules.ThesisManagement.Application.Storage;

public sealed class ThesisSubmissionImageValidator : IThesisSubmissionImageValidator
{
    public const long MaximumImageFileSizeBytes = 5 * 1024 * 1024;
    public const int MaximumEvidenceImageCount = 5;

    private const int BufferSize = 64 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public async Task<ValidatedThesisSubmissionFile> ValidateAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var fileExtension = ValidateFileName(fileName);

        if (content is null || !content.CanRead)
        {
            throw new ThesisSubmissionFileValidationException("Không thể đọc nội dung ảnh minh chứng tải lên.");
        }

        if (content.CanSeek && content.Length - content.Position > MaximumImageFileSizeBytes)
        {
            throw ImageTooLarge();
        }

        var imageBytes = await ReadBoundedContentAsync(content, cancellationToken);
        if (imageBytes.Length == 0)
        {
            throw new ThesisSubmissionFileValidationException("Tệp ảnh minh chứng rỗng.");
        }

        ValidateContentSignature(fileExtension, imageBytes);
        var checksum = Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant();

        return new ValidatedThesisSubmissionFile(
            fileName,
            fileExtension,
            GetContentType(fileExtension),
            imageBytes.Length,
            checksum,
            imageBytes);
    }

    private static string ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal)
            || fileName.IndexOfAny(['/', '\\']) >= 0
            || fileName.Any(char.IsControl))
        {
            throw new ThesisSubmissionFileValidationException("Tên tệp ảnh minh chứng không hợp lệ.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName))
            || extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            throw new ThesisSubmissionFileValidationException("Chỉ chấp nhận ảnh minh chứng PNG, JPG, JPEG hoặc WEBP.");
        }

        return extension;
    }

    private static async Task<byte[]> ReadBoundedContentAsync(Stream content, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long totalRead = 0;

            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
                if (totalRead > MaximumImageFileSizeBytes)
                {
                    throw ImageTooLarge();
                }

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            return memory.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateContentSignature(string fileExtension, byte[] content)
    {
        var isValid = fileExtension switch
        {
            ".png" => content.AsSpan().StartsWith(PngSignature),
            ".jpg" or ".jpeg" => content.AsSpan().StartsWith(JpegSignature),
            ".webp" => content.Length >= 12
                && content.AsSpan(0, 4).SequenceEqual(RiffSignature)
                && content.AsSpan(8, 4).SequenceEqual(WebpSignature),
            _ => false
        };

        if (!isValid)
        {
            throw new ThesisSubmissionFileValidationException("Nội dung ảnh minh chứng không khớp với định dạng đã khai báo.");
        }
    }

    private static string GetContentType(string fileExtension)
        => fileExtension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new ArgumentOutOfRangeException(nameof(fileExtension), fileExtension, "Unsupported image extension.")
        };

    private static ThesisSubmissionFileValidationException ImageTooLarge()
        => new($"Dung lượng mỗi ảnh minh chứng không được vượt quá {MaximumImageFileSizeBytes / 1024 / 1024} MB.");
}

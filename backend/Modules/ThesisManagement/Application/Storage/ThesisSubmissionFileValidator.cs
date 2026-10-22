using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace UGTM.Modules.ThesisManagement.Application.Storage;

public sealed class ThesisSubmissionFileValidator : IThesisSubmissionFileValidator
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;

    private const int BufferSize = 80 * 1024;
    private const int MaximumDocxEntries = 1_000;
    private const long MaximumDocxDeclaredUncompressedSizeBytes = 100 * 1024 * 1024;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] LegacyWordSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = "PK\x03\x04"u8.ToArray();

    public async Task<ValidatedThesisSubmissionFile> ValidateAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var fileExtension = ValidateFileName(fileName);

        if (content is null || !content.CanRead)
        {
            throw new ThesisSubmissionFileValidationException("Không thể đọc nội dung tệp tải lên.");
        }

        if (content.CanSeek && content.Length - content.Position > MaximumFileSizeBytes)
        {
            throw FileTooLarge();
        }

        var fileContent = await ReadBoundedContentAsync(content, cancellationToken);
        ValidateContentSignature(fileExtension, fileContent);

        var checksum = Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();

        return new ValidatedThesisSubmissionFile(
            fileName,
            fileExtension,
            GetContentType(fileExtension),
            fileContent.Length,
            checksum,
            fileContent);
    }

    private static string ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal)
            || fileName.IndexOfAny(['/', '\\']) >= 0
            || fileName.Any(char.IsControl))
        {
            throw new ThesisSubmissionFileValidationException("Tên tệp không hợp lệ.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fileName))
            || extension is not (".pdf" or ".doc" or ".docx"))
        {
            throw new ThesisSubmissionFileValidationException("Chỉ chấp nhận tệp PDF, DOC hoặc DOCX.");
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

                if (totalRead > MaximumFileSizeBytes)
                {
                    throw FileTooLarge();
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
            ".pdf" => HasPdfSignature(content),
            ".doc" => content.AsSpan().StartsWith(LegacyWordSignature),
            ".docx" => IsValidDocx(content),
            _ => false
        };

        if (!isValid)
        {
            throw new ThesisSubmissionFileValidationException("Nội dung tệp không khớp với định dạng đã khai báo.");
        }
    }

    private static bool HasPdfSignature(ReadOnlySpan<byte> content)
    {
        var headerLength = Math.Min(content.Length, 1_024);
        return content[..headerLength].IndexOf(PdfSignature) >= 0;
    }

    private static bool IsValidDocx(byte[] content)
    {
        if (!content.AsSpan().StartsWith(ZipSignature))
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count > MaximumDocxEntries)
            {
                return false;
            }

            var declaredUncompressedSize = 0L;
            var hasContentTypes = false;
            var hasDocument = false;

            foreach (var entry in archive.Entries)
            {
                declaredUncompressedSize += entry.Length;

                if (declaredUncompressedSize > MaximumDocxDeclaredUncompressedSizeBytes)
                {
                    return false;
                }

                hasContentTypes |= string.Equals(entry.FullName, "[Content_Types].xml", StringComparison.Ordinal);
                hasDocument |= string.Equals(entry.FullName, "word/document.xml", StringComparison.Ordinal);
            }

            return hasContentTypes && hasDocument;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string GetContentType(string fileExtension)
        => fileExtension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => throw new ArgumentOutOfRangeException(nameof(fileExtension), fileExtension, "Unsupported file extension.")
        };

    private static ThesisSubmissionFileValidationException FileTooLarge()
        => new($"Dung lượng tệp không được vượt quá {MaximumFileSizeBytes / 1024 / 1024} MB.");
}

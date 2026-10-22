using System.IO.Compression;
using System.Text;
using UGTM.Modules.ThesisManagement.Application.Storage;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisSubmissionFileValidatorTests
{
    private readonly ThesisSubmissionFileValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_accepts_pdf_with_a_matching_signature_and_returns_trusted_metadata()
    {
        var content = Encoding.ASCII.GetBytes("%PDF-1.7\nminimal thesis document");

        var result = await _validator.ValidateAsync("de-cuong.PDF", new MemoryStream(content));

        Assert.Equal("de-cuong.PDF", result.FileName);
        Assert.Equal(".pdf", result.FileExtension);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(content.Length, result.FileSize);
        Assert.Equal("9611e69570861d7ee5b0ec0cbc27550e6890da2541b69eb7210628818d8ff575", result.ChecksumSha256);
        Assert.Equal(content, result.Content.ToArray());
    }

    [Fact]
    public async Task ValidateAsync_accepts_legacy_word_document_with_compound_file_signature()
    {
        byte[] content = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x01];

        var result = await _validator.ValidateAsync("de-cuong.doc", new MemoryStream(content));

        Assert.Equal("application/msword", result.ContentType);
        Assert.Equal(".doc", result.FileExtension);
    }

    [Fact]
    public async Task ValidateAsync_accepts_docx_with_required_openxml_entries()
    {
        var content = CreateDocxContent();

        var result = await _validator.ValidateAsync("bao-cao.docx", new MemoryStream(content));

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.Equal(".docx", result.FileExtension);
    }

    [Theory]
    [InlineData("de-cuong.pdf")]
    [InlineData("de-cuong.doc")]
    [InlineData("de-cuong.docx")]
    public async Task ValidateAsync_rejects_content_that_does_not_match_its_extension(string fileName)
    {
        var content = Encoding.UTF8.GetBytes("this is not a supported thesis document");

        await Assert.ThrowsAsync<ThesisSubmissionFileValidationException>(
            () => _validator.ValidateAsync(fileName, new MemoryStream(content)));
    }

    [Theory]
    [InlineData("../de-cuong.pdf")]
    [InlineData("thu_muc\\de-cuong.pdf")]
    [InlineData("de-cuong.exe")]
    [InlineData(" de-cuong.pdf")]
    public async Task ValidateAsync_rejects_unsafe_or_unsupported_file_names(string fileName)
    {
        var content = Encoding.ASCII.GetBytes("%PDF-1.7");

        await Assert.ThrowsAsync<ThesisSubmissionFileValidationException>(
            () => _validator.ValidateAsync(fileName, new MemoryStream(content)));
    }

    [Fact]
    public async Task ValidateAsync_rejects_files_larger_than_twenty_mebibytes()
    {
        var content = new byte[ThesisSubmissionFileValidator.MaximumFileSizeBytes + 1];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(content, 0);

        await Assert.ThrowsAsync<ThesisSubmissionFileValidationException>(
            () => _validator.ValidateAsync("de-cuong.pdf", new MemoryStream(content)));
    }

    private static byte[] CreateDocxContent()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", "<Types />");
            WriteEntry(archive, "word/document.xml", "<w:document />");
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);

        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}

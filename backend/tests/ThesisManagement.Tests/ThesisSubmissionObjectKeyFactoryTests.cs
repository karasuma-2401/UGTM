using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisSubmissionObjectKeyFactoryTests
{
    private readonly ThesisSubmissionObjectKeyFactory _factory = new();

    [Fact]
    public void Create_generates_an_opaque_versioned_key_without_the_original_file_name()
    {
        var entityId = Guid.Parse("3c6bc3bd-14bd-4f80-8fce-d4b54ca4f93c");

        var key = _factory.Create(SubmissionEntityType.Syllabus, entityId, 2, ".PDF");

        Assert.Matches(
            "^thesis-submissions/syllabus/3c6bc3bd14bd4f808fced4b54ca4f93c/v2/[0-9a-f]{32}\\.pdf$",
            key);
        Assert.DoesNotContain("de-cuong", key, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData("pdf")]
    [InlineData(".pdf/../secret")]
    public void Create_rejects_an_extension_that_was_not_validated(string fileExtension)
    {
        Assert.Throws<ArgumentException>(
            () => _factory.Create(SubmissionEntityType.Syllabus, Guid.NewGuid(), 1, fileExtension));
    }
}

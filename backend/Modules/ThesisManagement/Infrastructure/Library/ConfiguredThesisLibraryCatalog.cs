using Microsoft.Extensions.Options;
using UGTM.Modules.ThesisManagement.Application.Library;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Library;

public sealed class ThesisLibraryOptions
{
    public const string SectionName = "ThesisLibrary";
    public ThesisLibraryPublication[] Publications { get; set; } = [];
    public bool IsValid() => Publications is { Length: <= 10000 } && Publications.All(p => p is not null && p.IsValid())
        && Publications.Select(p => p.Slug).Distinct(StringComparer.Ordinal).Count() == Publications.Length;
}

internal sealed class ConfiguredThesisLibraryCatalog(IOptions<ThesisLibraryOptions> options) : IThesisLibraryCatalog
{
    public IReadOnlyList<ThesisLibraryPublication> Publications => options.Value.Publications;
}

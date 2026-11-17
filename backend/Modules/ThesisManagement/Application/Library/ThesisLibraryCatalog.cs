using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Library;

// This is an explicit editorial publication, never a projection of every archived submission.
// Only approved display metadata belongs here; identifiers used to locate private files stay server-side.
public sealed class ThesisLibraryPublication
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Abstract { get; set; } = "";
    public string[] Authors { get; set; } = [];
    public string Advisor { get; set; } = "";
    public string Department { get; set; } = "";
    public string AcademicYear { get; set; } = "";
    public string Semester { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public DateTimeOffset PublishedAt { get; set; }
    public Guid? SubmissionId { get; set; }
    public int? PackageVersion { get; set; }
    public string? ManifestSha256 { get; set; }

    public bool IsValid() => Slug is not null && Regex.IsMatch(Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)
        && Slug.Length <= 100 && Text(Title, 500) && Text(Abstract, 10000) && Text(Advisor, 200)
        && Text(Department, 100) && AcademicYear is not null && Regex.IsMatch(AcademicYear, "^[0-9]{4}-[0-9]{4}$")
        && int.Parse(AcademicYear[5..], CultureInfo.InvariantCulture) == int.Parse(AcademicYear[..4], CultureInfo.InvariantCulture) + 1
        && Text(Semester, 100) && Authors is { Length: > 0 and <= 10 } && Authors.All(a => Text(a, 200))
        && Keywords is { Length: <= 20 } && Keywords.All(k => Text(k, 100)) && Keywords.Distinct(StringComparer.OrdinalIgnoreCase).Count() == Keywords.Length
        && PublishedAt != default
        && (SubmissionId is null && PackageVersion is null && ManifestSha256 is null
            || SubmissionId is { } id && id != Guid.Empty && PackageVersion is > 0 && ManifestSha256 is not null
            && Regex.IsMatch(ManifestSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant));
    private static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Contains('\0');
    public PublicThesisDto ToPublic() => new(Slug, Title, Abstract, Authors, Advisor, Department, AcademicYear,
        Semester, Keywords, PublishedAt, SubmissionId.HasValue);
}

public sealed record PublicThesisDto(string Slug, string Title, string Abstract, IReadOnlyList<string> Authors,
    string Advisor, string Department, string AcademicYear, string Semester, IReadOnlyList<string> Keywords,
    DateTimeOffset PublishedAt, bool HasDocument);
public sealed record PublicThesisPageDto(IReadOnlyList<PublicThesisDto> Items, int TotalCount, int PageNumber, int PageSize,
    IReadOnlyList<string> Years, IReadOnlyList<string> Semesters, IReadOnlyList<string> Departments);
public sealed record ThesisLibraryQuery(string? Q = null, string? Year = null, string? Semester = null,
    string? Department = null, string Sort = "newest", int Page = 1, int PageSize = 12);
public interface IThesisLibraryCatalog { IReadOnlyList<ThesisLibraryPublication> Publications { get; } }

public static class ThesisLibrarySearch
{
    public static readonly Error InvalidQuery = Error.Validation("ThesisLibrary.InvalidQuery", "Bộ lọc hoặc trang tra cứu không hợp lệ.");
    public static readonly Error NotFound = Error.NotFound("ThesisLibrary.NotFound", "Không tìm thấy khóa luận được công bố.");
    public static IReadOnlyList<ThesisLibraryPublication> Published(IThesisLibraryCatalog catalog, DateTimeOffset now)
        => catalog.Publications.Where(p => p.PublishedAt <= now).ToArray();
    public static Result<PublicThesisPageDto> Search(IThesisLibraryCatalog catalog, ThesisLibraryQuery query, DateTimeOffset now)
    {
        if (query.Q?.Length > 200 || query.Year?.Length > 100 || query.Semester?.Length > 100 || query.Department?.Length > 100
            || query.Sort is not ("newest" or "title") || query.Page is < 1 or > 1000000 || query.PageSize is < 1 or > 24)
            return Result.Failure<PublicThesisPageDto>(InvalidQuery);
        var all = Published(catalog, now);
        var normalized = Fold(query.Q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = all.Where(p => (string.IsNullOrEmpty(query.Year) || p.AcademicYear == query.Year)
            && (string.IsNullOrEmpty(query.Semester) || p.Semester == query.Semester)
            && (string.IsNullOrEmpty(query.Department) || p.Department == query.Department)
            && Matches(p, normalized)).ToArray();
        var ordered = query.Sort == "title"
            ? filtered.OrderBy(p => p.Title, StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), false)).ThenBy(p => p.Slug, StringComparer.Ordinal)
            : filtered.OrderByDescending(p => p.PublishedAt).ThenBy(p => p.Slug, StringComparer.Ordinal);
        var items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(p => p.ToPublic()).ToArray();
        return Result.Success(new PublicThesisPageDto(items, filtered.Length, query.Page, query.PageSize,
            all.Select(p => p.AcademicYear).Distinct().OrderDescending(StringComparer.Ordinal).ToArray(),
            all.Select(p => p.Semester).Distinct().Order(StringComparer.Ordinal).ToArray(),
            all.Select(p => p.Department).Distinct().Order(StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), false)).ToArray()));
    }
    private static bool Matches(ThesisLibraryPublication publication, string[] terms)
    {
        if (terms.Length == 0) return true;
        var corpus = Fold($"{publication.Title} {publication.Abstract} {string.Join(' ', publication.Authors)} {publication.Advisor} {string.Join(' ', publication.Keywords)}");
        return terms.All(term => corpus.Contains(term, StringComparison.Ordinal));
    }
    private static string Fold(string value)
    {
        var result = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(c));
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}

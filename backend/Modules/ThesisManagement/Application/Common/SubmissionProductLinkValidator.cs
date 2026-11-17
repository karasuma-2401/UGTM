using System.Text.Json;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.Common;

public static class SubmissionProductLinkValidator
{
    public const int MaximumLinkCount = 5;
    public const int MaximumUrlLength = 2048;
    public const int MaximumLabelLength = 80;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record NormalizedProductLink(
        SubmissionLinkCategory Category,
        string? Label,
        string Url);

    public static bool TryParseJson(
        string? rawJson,
        out IReadOnlyList<SubmissionProductLinkInput>? links)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            links = null;
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<SubmissionProductLinkInput>>(rawJson, JsonOptions);
            if (parsed is null)
            {
                links = null;
                return false;
            }

            links = parsed;
            return true;
        }
        catch (JsonException)
        {
            links = null;
            return false;
        }
    }

    public static bool TryNormalize(
        IReadOnlyList<SubmissionProductLinkInput>? inputs,
        string? fallbackGitUrl,
        bool requireAtLeastOne,
        out IReadOnlyList<NormalizedProductLink> normalized)
    {
        normalized = [];
        var list = new List<NormalizedProductLink>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (inputs is { Count: > 0 })
        {
            if (inputs.Count > MaximumLinkCount)
            {
                return false;
            }

            foreach (var item in inputs)
            {
                if (!TryNormalizeHttpUrl(item.Url, out var normalizedUrl))
                {
                    return false;
                }

                if (!TryNormalizeCategory(item.Category, out var category))
                {
                    return false;
                }

                string? normalizedLabel = null;
                if (!string.IsNullOrWhiteSpace(item.Label))
                {
                    var trimmedLabel = item.Label.Trim();
                    if (trimmedLabel.Length > MaximumLabelLength || trimmedLabel.Any(char.IsControl))
                    {
                        return false;
                    }
                    normalizedLabel = trimmedLabel;
                }

                if (!seenUrls.Add(normalizedUrl!))
                {
                    return false;
                }

                list.Add(new NormalizedProductLink(category, normalizedLabel, normalizedUrl!));
            }
        }
        else if (!string.IsNullOrWhiteSpace(fallbackGitUrl))
        {
            if (!TryNormalizeHttpUrl(fallbackGitUrl, out var normalizedUrl))
            {
                return false;
            }

            list.Add(new NormalizedProductLink(SubmissionLinkCategory.SourceCode, null, normalizedUrl!));
        }

        if (requireAtLeastOne && list.Count == 0)
        {
            return false;
        }

        normalized = list;
        return true;
    }

    public static bool TryNormalizeHttpUrl(string? value, out string? normalizedUrl)
    {
        normalizedUrl = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length > MaximumUrlLength)
        {
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool TryNormalizeCategory(string? value, out SubmissionLinkCategory category)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            category = SubmissionLinkCategory.Other;
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out category)
            && Enum.IsDefined(category);
    }
}

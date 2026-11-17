using System.Text.Json;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI;

public static class PlagiarismReportParser
{
    public static (PlagiarismSummaryDto? Summary, List<PlagiarismFindingDto> Findings, string? Disclaimer) Parse(JsonElement root)
    {
        PlagiarismSummaryDto? summary = null;
        if (root.TryGetProperty("summary", out var summaryEl) && summaryEl.ValueKind == JsonValueKind.Object)
        {
            var coverage = GetDouble(summaryEl, "similarity_coverage") ?? GetDouble(root, "similarity_coverage") ?? 0;
            var maxScore = GetDouble(summaryEl, "max_reranker_score") ?? GetDouble(root, "max_rerank_score") ?? GetDouble(root, "max_score") ?? 0;
            var scannedChunks = GetInt(summaryEl, "scanned_chunks") ?? GetInt(root, "scanned_chunks") ?? 0;
            var scannedChars = GetInt(summaryEl, "scanned_characters") ?? GetInt(root, "scanned_characters") ?? 0;
            var matchedChars = GetInt(summaryEl, "matched_characters") ?? GetInt(root, "matched_characters") ?? 0;
            var citedCount = GetInt(summaryEl, "cited_findings") ?? GetInt(root, "cited_findings") ?? 0;
            var uncitedCount = GetInt(summaryEl, "uncited_findings") ?? GetInt(root, "uncited_findings") ?? 0;
            var suspectedCount = GetInt(summaryEl, "suspected_findings") ?? GetInt(root, "suspected_findings");

            summary = new PlagiarismSummaryDto(
                coverage,
                maxScore,
                scannedChunks,
                scannedChars,
                matchedChars,
                citedCount,
                uncitedCount,
                suspectedCount);
        }

        var findings = new List<PlagiarismFindingDto>();
        if (root.TryGetProperty("findings", out var findingsEl) && findingsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in findingsEl.EnumerateArray())
            {
                var passageGroupId = GetString(item, "passage_group_id");
                var subChunkNo = GetInt(item, "submission_chunk_no") ?? GetInt(item, "submission_chunk_id_int");
                var subPage = GetInt(item, "submission_page");
                var subStart = GetInt(item, "submission_start") ?? GetInt(item, "char_start");
                var subEnd = GetInt(item, "submission_end") ?? GetInt(item, "char_end");
                var subText = GetString(item, "submission_text") ?? string.Empty;

                var srcKey = GetString(item, "source_key") ?? GetString(item, "source_id");
                var srcVer = GetInt(item, "source_version");
                var srcFilename = GetString(item, "source_filename");
                var srcPage = GetInt(item, "source_page");
                var srcText = GetString(item, "source_text") ?? string.Empty;

                var rerankerScore = GetDouble(item, "reranker_score") ?? GetDouble(item, "score");
                var classifierScore = GetDouble(item, "classifier_score");
                var citationStatus = GetString(item, "citation_status");
                var citationExplanation = GetString(item, "explanation") ?? GetString(item, "citation_explanation");
                var potentialMisattribution = GetBool(item, "potential_misattribution") ?? false;
                var reviewRequired = GetBool(item, "review_required") ?? true;

                findings.Add(new PlagiarismFindingDto(
                    passageGroupId,
                    subChunkNo,
                    subPage,
                    subStart,
                    subEnd,
                    subText,
                    srcKey,
                    srcVer,
                    srcFilename,
                    srcPage,
                    srcText,
                    rerankerScore,
                    classifierScore,
                    citationStatus,
                    citationExplanation,
                    potentialMisattribution,
                    reviewRequired));
            }
        }

        var disclaimer = GetString(root, "disclaimer");

        return (summary, findings, disclaimer);
    }

    private static double? GetDouble(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetDouble();
        }
        return null;
    }

    private static int? GetInt(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return null;
    }

    private static string? GetString(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static bool? GetBool(JsonElement el, string propertyName)
    {
        if (el.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }
}


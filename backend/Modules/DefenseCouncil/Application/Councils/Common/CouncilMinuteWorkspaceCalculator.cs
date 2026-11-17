using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Grading;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Common;

public static class CouncilMinuteWorkspaceCalculator
{
    public sealed record ItemEvaluationData(
        string ComponentCode,
        string RoleLabel,
        string LecturerName,
        decimal? Score,
        decimal Weight,
        string Category,
        bool IsDualRoleReviewer,
        bool IsIgnoredInCalculation,
        string? ExclusionReason = null,
        Guid SourceId = default);

    public static string SourceFor(string code) => code switch
    {
        "ADVISOR" => "Advisor",
        "REVIEWER" => "Reviewer",
        "COUNCIL_CHAIRMAN" => "CouncilChairman",
        "COUNCIL_SECRETARY" => "CouncilSecretary",
        "COUNCIL_MEMBER" => "CouncilMember",
        _ => code
    };

    public static (
        IReadOnlyList<MinuteScoreItemDto> ScoreItems,
        MinuteDiscrepancySummaryDto DiscrepancySummary,
        MinuteCalculationPreviewDto CalculationPreview
    ) BuildWorkspaceData(
        IEnumerable<ItemEvaluationData> items,
        string? savedSecretaryMarking,
        string? requestedSecretaryMarking,
        string? requestedTargetComponentCode,
        string? overrideReason,
        ScoreSummaryConfigurationDto? configuration = null)
    {
        var rawItemList = items.ToList();
        var inside = rawItemList.Any(i => i.ComponentCode == "REVIEWER" && i.IsDualRoleReviewer);

        // Xác định marking và target code được áp dụng
        var secretaryMarking = requestedSecretaryMarking ?? savedSecretaryMarking ?? "None";
        var isRejection = string.Equals(secretaryMarking, "RejectedAsPerMinute", StringComparison.OrdinalIgnoreCase);
        var isAccepted = string.Equals(secretaryMarking, "AcceptedAsPerMinute", StringComparison.OrdinalIgnoreCase);

        // Áp dụng trạng thái ignore vào từng item
        var processedItems = new List<MinuteScoreItemDto>();
        string? activeIgnoredCode = null;

        foreach (var it in rawItemList)
        {
            var isIgnored = it.IsIgnoredInCalculation;

            if (isRejection)
            {
                isIgnored = string.Equals(it.ComponentCode, requestedTargetComponentCode, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(it.RoleLabel, requestedTargetComponentCode, StringComparison.OrdinalIgnoreCase);
            }
            else if (isAccepted)
            {
                isIgnored = false;
            }

            if (isIgnored)
            {
                activeIgnoredCode = it.ComponentCode;
            }

            string statusText;
            if (it.IsDualRoleReviewer && it.ComponentCode == "COUNCIL_MEMBER")
            {
                statusText = "Chỉ tham dự";
            }
            else if (it.Score.HasValue)
            {
                statusText = "Đã chấm";
            }
            else
            {
                statusText = "Chưa chấm";
            }

            processedItems.Add(new MinuteScoreItemDto(
                it.ComponentCode,
                it.RoleLabel,
                it.LecturerName,
                it.Score,
                it.IsDualRoleReviewer && it.ComponentCode == "COUNCIL_MEMBER" ? 0m :
                    configuration?.Components.Where(c => ConfiguredGradeComponents.Applies(c, inside) && c.Source == SourceFor(it.ComponentCode)).Sum(c => c.Weight) ?? 0m,
                statusText,
                it.Category,
                it.IsDualRoleReviewer,
                isIgnored));
        }

        // Tính toán sai biệt điểm
        // Điểm so sánh lấy từ các thành viên hội đồng có điểm tại buổi bảo vệ (bao gồm GVPB kiêm nhiệm)
        var defenseScores = rawItemList
            .Where(i => i.Score.HasValue && (i.ComponentCode.StartsWith("COUNCIL_") || (i.IsDualRoleReviewer && i.ComponentCode == "REVIEWER")))
            .Select(i => i.Score!.Value)
            .ToList();

        var hasDiscrepancy = false;
        decimal? scoreDifference = null;
        if (defenseScores.Count >= 2)
        {
            var max = defenseScores.Max();
            var min = defenseScores.Min();
            scoreDifference = Math.Round(max - min, 2);
            hasDiscrepancy = scoreDifference >= 2.0m;
        }

        // Xác định SecretaryMarking và ChairmanConclusion chuẩn hóa
        string finalSecretaryMarking;
        if (isRejection || activeIgnoredCode != null)
        {
            finalSecretaryMarking = "RejectedAsPerMinute";
        }
        else if (isAccepted || string.Equals(overrideReason, "ACCEPTED_AS_PER_MINUTE", StringComparison.OrdinalIgnoreCase))
        {
            finalSecretaryMarking = "AcceptedAsPerMinute";
        }
        else
        {
            finalSecretaryMarking = hasDiscrepancy ? "None" : "AcceptedAsPerMinute";
        }

        string chairmanConclusion;
        if (activeIgnoredCode != null)
        {
            var ignoredItem = processedItems.FirstOrDefault(i => i.ComponentCode == activeIgnoredCode);
            var roleText = ignoredItem?.RoleLabel ?? activeIgnoredCode;
            chairmanConclusion = $"Không công nhận điểm {roleText} theo biên bản hội đồng";
        }
        else if (finalSecretaryMarking == "AcceptedAsPerMinute")
        {
            chairmanConclusion = hasDiscrepancy
                ? "Giữ nguyên các đầu điểm theo biên bản hội đồng"
                : "Các đầu điểm hợp lệ (không có sai biệt >= 2.0)";
        }
        else
        {
            chairmanConclusion = "Phát hiện sai biệt điểm >= 2.0, cần Hội đồng thảo luận và kết luận";
        }

        var discrepancySummary = new MinuteDiscrepancySummaryDto(
            hasDiscrepancy,
            scoreDifference,
            chairmanConclusion,
            finalSecretaryMarking,
            activeIgnoredCode,
            activeIgnoredCode != null);

        var sources = rawItemList.Select((item, index) => (item, index))
            .Where(value => !(value.item.ComponentCode == "COUNCIL_MEMBER" && value.item.IsDualRoleReviewer))
            .Select(value => new ConfiguredGradeSource(value.item.SourceId, SourceFor(value.item.ComponentCode),
                value.item.Score, processedItems[value.index].IsIgnoredInCalculation, value.item.ExclusionReason)).ToArray();
        var adjusted = ConfiguredGradeComponents.Resolve(configuration, inside, sources);
        var raw = ConfiguredGradeComponents.Resolve(configuration, inside, sources, includeExcluded: true);
        var allRequiredScoresPresent = sources.Length > 0 && sources.All(source => source.Score.HasValue);
        string? errorCode = adjusted.IsFailure ? adjusted.Error.Code : raw.IsFailure ? raw.Error.Code :
            !allRequiredScoresPresent ? "ScoreSummary.MissingRequiredSource" : null;
        decimal rawTotal = 0m, rawDivisor = 0m, rawAverage = 0m;
        decimal adjustedTotal = 0m, adjustedDivisor = 0m, adjustedAverage = 0m;
        decimal? finalRoundedScore = null;
        var formulaExplanation = "Chưa có đủ điểm thành phần hoặc cấu hình Active hợp lệ để tính điểm.";
        if (errorCode is null)
        {
            try
            {
                var calculator = new ThesisGradeCalculator();
                var rawGrade = calculator.Calculate(raw.Value.Components.Select(c => new GradeContribution(c.ComponentCode, c.Score, c.Weight)));
                var adjustedGrade = calculator.Calculate(adjusted.Value.Components.Select(c => new GradeContribution(c.ComponentCode, c.Score, c.Weight, c.IsIgnoredInCalculation)));
                rawDivisor = rawGrade.TotalWeight;
                rawTotal = raw.Value.Components.Sum(c => c.Score * c.Weight);
                rawAverage = Math.Round(rawGrade.FinalScore, 6);
                adjustedDivisor = adjustedGrade.TotalWeight;
                adjustedTotal = adjusted.Value.Components.Where(c => !c.IsIgnoredInCalculation).Sum(c => c.Score * c.Weight);
                adjustedAverage = Math.Round(adjustedGrade.FinalScore, 6);
                finalRoundedScore = Math.Round(adjustedGrade.FinalScore, 2, MidpointRounding.AwayFromZero);
                var parts = adjusted.Value.Components.Where(c => !c.IsIgnoredInCalculation)
                    .Select(c => $"{c.Score:G29}×{c.Weight:G29} [{c.ComponentName}]");
                formulaExplanation = $"Cấu hình v{configuration!.VersionNo}: ({string.Join(" + ", parts)}) / {adjustedDivisor:G29}; điểm làm tròn: {finalRoundedScore:F2}";
                foreach (var component in adjusted.Value.Components.Where(c => c.Source == "CouncilAverage"))
                    processedItems.Add(new MinuteScoreItemDto(component.ComponentCode, component.ComponentName, "Hội đồng",
                        component.Score, component.Weight, "Đã chấm", "Trung bình HĐ", false, false));
            }
            catch (InvalidOperationException) { errorCode = "ScoreSummary.NoPositiveWeight"; }
            catch (Exception error) when (error is ArgumentException or OverflowException) { errorCode = "ScoreSummary.ConfigurationInvalid"; }
        }
        var discrepancyResolved = !hasDiscrepancy || finalSecretaryMarking is "RejectedAsPerMinute" or "AcceptedAsPerMinute";
        var calculationPreview = new MinuteCalculationPreviewDto(rawTotal, rawDivisor, rawAverage,
            adjustedTotal, adjustedDivisor, adjustedAverage, finalRoundedScore, formulaExplanation,
            errorCode is null && discrepancyResolved && adjustedDivisor > 0,
            configuration?.Id, configuration?.VersionNo, errorCode);

        return (processedItems, discrepancySummary, calculationPreview);
    }
}

using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

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
        string? ExclusionReason = null);

    public static (
        IReadOnlyList<MinuteScoreItemDto> ScoreItems,
        MinuteDiscrepancySummaryDto DiscrepancySummary,
        MinuteCalculationPreviewDto CalculationPreview
    ) BuildWorkspaceData(
        IEnumerable<ItemEvaluationData> items,
        string? savedSecretaryMarking,
        string? requestedSecretaryMarking,
        string? requestedTargetComponentCode,
        string? overrideReason)
    {
        var rawItemList = items.ToList();

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
                it.Weight,
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

        // Tính calculation_preview
        // Raw: tính trên các item có weight > 0 và có điểm
        // Chú ý: Raw bao gồm cả item bị ignore nếu có điểm
        decimal rawTotal = 0m;
        decimal rawDivisor = 0m;
        var rawParts = new List<string>();

        foreach (var item in processedItems.Where(i => i.Weight > 0 && i.Score.HasValue))
        {
            var contribution = item.Score!.Value * item.Weight;
            rawTotal += contribution;
            rawDivisor += item.Weight;
            rawParts.Add($"{item.Score.Value:G29}×{item.Weight:G29} [{item.RoleLabel}]");
        }

        var rawAverage = rawDivisor > 0 ? Math.Round(rawTotal / rawDivisor, 6) : 0m;

        // Adjusted: tính trên các item có weight > 0, có điểm và IsIgnoredInCalculation == false
        decimal adjustedTotal = 0m;
        decimal adjustedDivisor = 0m;
        var adjustedParts = new List<string>();

        foreach (var item in processedItems.Where(i => i.Weight > 0 && i.Score.HasValue && !i.IsIgnoredInCalculation))
        {
            var contribution = item.Score!.Value * item.Weight;
            adjustedTotal += contribution;
            adjustedDivisor += item.Weight;
            adjustedParts.Add($"{item.Score.Value:G29}×{item.Weight:G29} [{item.RoleLabel}]");
        }

        var adjustedAverage = adjustedDivisor > 0 ? Math.Round(adjustedTotal / adjustedDivisor, 6) : 0m;
        decimal? finalRoundedScore = adjustedDivisor > 0 ? Math.Round(adjustedTotal / adjustedDivisor, 2) : null;

        // Formula explanation
        string formulaExplanation;
        var rawExpr = rawParts.Count > 0
            ? $"({string.Join(" + ", rawParts)}) / {rawDivisor:G29} = {rawTotal:G29} / {rawDivisor:G29} ≈ {rawAverage:F6}"
            : "Chưa có đủ điểm thành phần";

        if (activeIgnoredCode != null)
        {
            var ignoredItem = processedItems.FirstOrDefault(i => i.ComponentCode == activeIgnoredCode);
            var roleText = ignoredItem?.RoleLabel ?? activeIgnoredCode;

            var adjExpr = adjustedParts.Count > 0
                ? $"({string.Join(" + ", adjustedParts)}) / {adjustedDivisor:G29} = {adjustedTotal:G29} / {adjustedDivisor:G29} = {adjustedAverage:G29} (Điểm làm tròn: {finalRoundedScore:F1})"
                : "Không có đầu điểm hợp lệ sau điều chỉnh";

            formulaExplanation = $"Trước điều chỉnh: {rawExpr} | Sau điều chỉnh (loại {roleText}): {adjExpr}";
        }
        else
        {
            var roundedText = finalRoundedScore.HasValue ? $" (Điểm làm tròn: {finalRoundedScore:F1})" : "";
            formulaExplanation = $"Công thức tính: {rawExpr}{roundedText}";
        }

        // is_ready_to_lock:
        // 1. Đủ điểm cho tất cả các item có weight > 0
        // 2. Nếu có sai biệt điểm (>= 2.0), secretaryMarking phải được chọn (RejectedAsPerMinute hoặc AcceptedAsPerMinute)
        var allRequiredScoresPresent = processedItems.Where(i => i.Weight > 0).All(i => i.Score.HasValue);
        var discrepancyResolved = !hasDiscrepancy || finalSecretaryMarking is "RejectedAsPerMinute" or "AcceptedAsPerMinute";
        var isReadyToLock = allRequiredScoresPresent && discrepancyResolved && adjustedDivisor > 0;

        var calculationPreview = new MinuteCalculationPreviewDto(
            rawTotal,
            rawDivisor,
            rawAverage,
            adjustedTotal,
            adjustedDivisor,
            adjustedAverage,
            finalRoundedScore,
            formulaExplanation,
            isReadyToLock);

        return (processedItems, discrepancySummary, calculationPreview);
    }
}

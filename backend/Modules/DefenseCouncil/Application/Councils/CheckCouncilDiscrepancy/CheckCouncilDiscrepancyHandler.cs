using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Users;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.CheckCouncilDiscrepancy;

public sealed class CheckCouncilDiscrepancyHandler(
    ICouncilRepository councilRepository,
    ICouncilUserInfoReader councilUserInfoReader)
{
    private const decimal WarningThreshold = 2.0m;

    public async Task<Result<CouncilDiscrepancyResponseDto>> HandleAsync(
        CheckCouncilDiscrepancyQuery query,
        CancellationToken ct = default)
    {
        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(query.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilDiscrepancyResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var councilScores = await councilRepository.GetCouncilScoresWithDetailsByAssignmentIdAsync(query.AssignmentId, ct);
        var scoreMap = councilScores.ToDictionary(s => s.CouncilMemberId);

        var memberUserIds = assignment.Council.Members.Select(m => m.UserId).Distinct().ToList();
        var userDict = await councilUserInfoReader.GetUsersByIdsAsync(memberUserIds, ct);

        var activeReviewer = await councilRepository.GetActiveReviewerAssignmentByGroupIdAsync(assignment.GroupId, ct);

        var memberScores = new List<MemberScoreDetailDto>();

        foreach (var member in assignment.Council.Members.OrderBy(m => m.RoleInCouncil))
        {
            userDict.TryGetValue(member.UserId, out var userInfo);
            var fullName = userInfo?.FullName ?? "Giảng viên";
            var email = userInfo?.Email ?? "";
            var userCode = userInfo?.UserCode;

            var isReviewer = activeReviewer != null && member.UserId == activeReviewer.ReviewerId;

            if (isReviewer)
            {
                // GVPB kiêm nhiệm: Điểm của giảng viên này lấy từ phiếu phản biện, bị khóa nhập điểm hội đồng
                memberScores.Add(new MemberScoreDetailDto(
                    member.Id,
                    member.UserId,
                    fullName,
                    email,
                    userCode,
                    member.RoleInCouncil.ToString(),
                    activeReviewer?.Evaluation?.Score,
                    activeReviewer?.Evaluation?.Comment,
                    activeReviewer?.Evaluation?.EvaluatedAt,
                    IsReviewerInCouncil: true,
                    IsBlockedFromScoring: true));
            }
            else
            {
                scoreMap.TryGetValue(member.Id, out var cs);

                memberScores.Add(new MemberScoreDetailDto(
                    member.Id,
                    member.UserId,
                    fullName,
                    email,
                    userCode,
                    member.RoleInCouncil.ToString(),
                    cs?.Score,
                    cs?.Feedback,
                    cs?.EvaluatedAt,
                    IsReviewerInCouncil: false,
                    IsBlockedFromScoring: false));
            }
        }

        // Tập điểm so sánh độ lệch bao gồm các điểm chấm hợp lệ tại buổi bảo vệ
        var comparisonScores = memberScores
            .Where(m => m.Score.HasValue)
            .Select(m => m.Score!.Value)
            .ToList();

        decimal? maxScore = null;
        decimal? minScore = null;
        decimal? difference = null;
        var isDiscrepancy = false;
        string? warningMessage = null;

        if (comparisonScores.Count >= 2)
        {
            maxScore = comparisonScores.Max();
            minScore = comparisonScores.Min();
            difference = Math.Round(maxScore.Value - minScore.Value, 2);
            isDiscrepancy = difference >= WarningThreshold;

            if (isDiscrepancy)
            {
                warningMessage = $"Phát hiện độ lệch điểm giữa các thành viên hội đồng là {difference:F2} điểm (>= {WarningThreshold:F1}). Đề nghị Hội đồng và Thư ký tiến hành thảo luận lại.";
            }
        }
        else if (comparisonScores.Count == 1)
        {
            maxScore = comparisonScores[0];
            minScore = maxScore;
            difference = 0m;
        }

        var requiredCount = assignment.Council.Members.Count(m => activeReviewer == null || m.UserId != activeReviewer.ReviewerId);
        var allScoresSubmitted = councilScores.Count >= requiredCount;

        decimal? averageCouncilScore = councilScores.Count > 0
            ? Math.Round(councilScores.Average(s => s.Score), 2)
            : null;

        var summary = await councilRepository.GetScoreSummaryByGroupIdAsync(assignment.GroupId, ct);
        decimal? finalScore = summary?.FinalScore;

        return Result.Success(new CouncilDiscrepancyResponseDto(
            query.AssignmentId,
            assignment.CouncilId,
            assignment.GroupId,
            isDiscrepancy,
            WarningThreshold,
            difference,
            maxScore,
            minScore,
            warningMessage,
            allScoresSubmitted,
            averageCouncilScore,
            finalScore,
            memberScores));
    }
}

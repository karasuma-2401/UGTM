using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;

public sealed class SubmitCouncilScoreHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ThesisGradeCalculator gradeCalculator)
{
    public async Task<Result<CouncilScoreResponseDto>> HandleAsync(
        SubmitCouncilScoreCommand command,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.Forbidden);
        }

        var userId = currentUser.UserId.Value;

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(command.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        if (assignment.Council.Status != CouncilStatus.Active)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.NotActiveForScoring);
        }

        var member = assignment.Council.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.UserNotCouncilMember);
        }

        // Xử lý đặc thù GVPB kiêm Ủy viên Hội đồng (TH1 - BR-07)
        var activeReviewer = await councilRepository.GetActiveReviewerAssignmentByGroupIdAsync(assignment.GroupId, ct);
        if (activeReviewer != null && activeReviewer.ReviewerId == userId)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.ReviewerCouncilMemberCannotScoreTwice);
        }

        // Kiểm tra khóa biên bản
        if (assignment.Minute != null && assignment.Minute.LockStatus == CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.MinuteLocked);
        }

        if (command.Details == null || command.Details.Count == 0)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.CriteriaRequired);
        }

        if (command.Details.Select(d => d.CriteriaId).Distinct().Count() != command.Details.Count)
        {
            return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.DuplicateCriteriaInRequest);
        }

        var batchCriteria = await councilRepository.GetGradingCriteriaByBatchIdAsync(assignment.Council.BatchId, ct);
        var criteriaMap = batchCriteria.ToDictionary(c => c.Id);

        foreach (var item in command.Details)
        {
            if (!criteriaMap.TryGetValue(item.CriteriaId, out var criterion))
            {
                return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.CriteriaNotFound(item.CriteriaId));
            }

            if (item.Score < 0)
            {
                return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.CriteriaScoreNegative(criterion.CriteriaName));
            }

            if (item.Score > criterion.MaxScore)
            {
                return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.ScoreExceedsMaxScore(criterion.CriteriaName, item.Score, criterion.MaxScore));
            }
        }

        var submittedIds = command.Details.Select(d => d.CriteriaId).ToHashSet();
        foreach (var req in batchCriteria)
        {
            if (!submittedIds.Contains(req.Id))
            {
                return Result.Failure<CouncilScoreResponseDto>(CouncilErrors.MissingRequiredCriteria(req.CriteriaName));
            }
        }

        var totalScore = Math.Round(command.Details.Sum(d => d.Score), 2);
        var evaluatedAt = DateTime.UtcNow;

        var existingScore = await councilRepository.GetCouncilScoreAsync(command.AssignmentId, member.Id, ct);
        var scoreId = existingScore?.Id ?? Guid.NewGuid();

        var councilScore = new CouncilScore
        {
            Id = scoreId,
            CouncilAssignmentId = command.AssignmentId,
            CouncilMemberId = member.Id,
            Score = totalScore,
            Feedback = command.Feedback?.Trim(),
            EvaluatedAt = evaluatedAt
        };

        var detailsList = command.Details.Select(d => new EvaluationDetail
        {
            Id = Guid.NewGuid(),
            CriteriaId = d.CriteriaId,
            EvaluationId = scoreId,
            Score = d.Score
        }).ToList();

        await councilRepository.SaveCouncilScoreAsync(councilScore, detailsList, ct);

        // Kiểm tra xem tất cả thành viên bắt buộc đã chấm điểm chưa
        var requiredMembers = assignment.Council.Members
            .Where(m => activeReviewer == null || m.UserId != activeReviewer.ReviewerId)
            .ToList();

        var existingScores = await councilRepository.GetCouncilScoresWithDetailsByAssignmentIdAsync(command.AssignmentId, ct);
        var scoredMemberIds = existingScores.Select(s => s.CouncilMemberId).ToHashSet();
        scoredMemberIds.Add(member.Id);

        if (requiredMembers.All(m => scoredMemberIds.Contains(m.Id)))
        {
            await councilRepository.UpdateMinuteLockStatusAsync(command.AssignmentId, CouncilMinuteLockStatus.AllScoresEntered, ct);

            var allScores = existingScores
                .Where(s => s.CouncilMemberId != member.Id)
                .Select(s => s.Score)
                .Append(totalScore)
                .ToList();

            var averageCouncilScore = Math.Round(allScores.Average(), 2);

            var advisorEval = await councilRepository.GetAdvisorEvaluationByGroupIdAsync(assignment.GroupId, ct);
            if (advisorEval != null && activeReviewer?.Evaluation?.Score != null)
            {
                var advisorScore = advisorEval.Score;
                var reviewerScore = activeReviewer.Evaluation.Score.Value;
                var isReviewerInCouncil = assignment.Council.Members.Any(m => m.UserId == activeReviewer.ReviewerId);

                var chairmanMember = assignment.Council.Members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Chairman);
                var secretaryMember = assignment.Council.Members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Secretary);
                var otherMember = assignment.Council.Members.FirstOrDefault(m => m.RoleInCouncil == CouncilMemberRole.Member && (activeReviewer == null || m.UserId != activeReviewer.ReviewerId));

                var chairmanScore = member.Id == chairmanMember?.Id
                    ? totalScore
                    : (existingScores.FirstOrDefault(s => s.CouncilMemberId == chairmanMember?.Id)?.Score ?? 0m);

                var secretaryScore = member.Id == secretaryMember?.Id
                    ? totalScore
                    : (existingScores.FirstOrDefault(s => s.CouncilMemberId == secretaryMember?.Id)?.Score ?? 0m);

                decimal? memberScore = null;
                if (!isReviewerInCouncil && otherMember != null)
                {
                    memberScore = member.Id == otherMember.Id
                        ? totalScore
                        : (existingScores.FirstOrDefault(s => s.CouncilMemberId == otherMember.Id)?.Score ?? 0m);
                }

                try
                {
                    var calcResult = gradeCalculator.CalculateDefault(
                        advisorScore,
                        reviewerScore,
                        chairmanScore,
                        secretaryScore,
                        isReviewerInCouncil,
                        memberScore);

                    var summary = new ScoreSummary
                    {
                        Id = Guid.NewGuid(),
                        GroupId = assignment.GroupId,
                        AdvisorScore = advisorScore,
                        ReviewerScore = reviewerScore,
                        CouncilScore = averageCouncilScore,
                        FinalScore = Math.Round(calcResult.FinalScore, 2),
                        TotalWeight = calcResult.TotalWeight,
                        CalculatedAt = DateTime.UtcNow,
                        CalculationFormula = isReviewerInCouncil
                            ? "2*HD + 2*PB + 1*CT + 1*TK (/6)"
                            : "2*HD + 2*PB + 1*CT + 1*TK + 1*UV (/7)",
                        Status = ScoreSummaryStatus.Pending
                    };

                    List<ScoreSummaryComponent> components =
                    [
                        new() { Id = Guid.NewGuid(), ComponentCode = "ADVISOR", SourceKey = "ADVISOR", Score = advisorScore, Weight = 2m },
                        new() { Id = Guid.NewGuid(), ComponentCode = "REVIEWER", SourceKey = "REVIEWER", Score = reviewerScore, Weight = 2m },
                        new() { Id = Guid.NewGuid(), ComponentCode = "COUNCIL_CHAIRMAN", SourceKey = "COUNCIL_CHAIRMAN", Score = chairmanScore, Weight = 1m },
                        new() { Id = Guid.NewGuid(), ComponentCode = "COUNCIL_SECRETARY", SourceKey = "COUNCIL_SECRETARY", Score = secretaryScore, Weight = 1m },
                    ];

                    if (!isReviewerInCouncil && memberScore.HasValue)
                    {
                        components.Add(new() { Id = Guid.NewGuid(), ComponentCode = "COUNCIL_MEMBER", SourceKey = "COUNCIL_MEMBER", Score = memberScore.Value, Weight = 1m });
                    }

                    await councilRepository.SaveScoreSummaryAsync(summary, components, ct);
                }
                catch
                {
                    // Trường hợp điểm thành phần chưa hoàn thiện, bỏ qua tính điểm tổng
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        var detailDtos = detailsList.Select(d =>
        {
            var crit = criteriaMap[d.CriteriaId];
            return new EvaluationDetailResponseDto(d.Id, d.CriteriaId, crit.CriteriaCode, crit.CriteriaName, d.Score, crit.MaxScore);
        }).ToList();

        return Result.Success(new CouncilScoreResponseDto(
            councilScore.Id,
            command.AssignmentId,
            member.Id,
            userId,
            totalScore,
            councilScore.Feedback,
            evaluatedAt,
            detailDtos));
    }
}

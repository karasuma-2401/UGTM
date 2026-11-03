using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;

public sealed class SubmitCouncilScoreHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICouncilScoreWriteGuard writeGuard, TimeProvider clock)
{
    public Task<Result<CouncilScoreResponseDto>> HandleAsync(SubmitCouncilScoreCommand command, CancellationToken ct = default)
        => !currentUser.IsAuthenticated || !currentUser.UserId.HasValue
            ? Task.FromResult(Result.Failure<CouncilScoreResponseDto>(CouncilErrors.Forbidden))
            : writeGuard.ExecuteAsync(command.AssignmentId, token => HandleCoreAsync(command, token), ct);

    private async Task<Result<CouncilScoreResponseDto>> HandleCoreAsync(
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
        var evaluatedAt = clock.GetUtcNow().UtcDateTime;

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

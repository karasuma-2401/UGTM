using System.Text.Json;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class SaveReviewerEvaluationHandler(
    IReviewerAssignmentWorkflowContract workflow, IReviewerAssignmentTransactionCoordinator coordinator,
    IReviewerAssignmentRepository assignments, IReviewerEvaluationRepository evaluations,
    IReviewerEvaluationIdempotencyRepository receipts, IReviewerAssignmentBatchReader batches,
    ReviewerEvaluationAuthorization authorization, ReviewerEvaluationAudit audit, ICurrentUser currentUser, TimeProvider timeProvider,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    IDefenseGroupContract? defenseGroupContract = null,
    ITopicSummaryReader? topicSummaryReader = null)
{
    private const string Operation = "ReviewerEvaluation.Save";

    public async Task<Result<SaveReviewerEvaluationResponse>> HandleAsync(SaveReviewerEvaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.AuthenticationRequired);
        if (!ReviewerEvaluationAuthorization.CanWrite(currentUser.Roles))
            return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.AccessDenied);
        var normalized = ReviewerEvaluationInput.Normalize(command);
        if (normalized.IsFailure) return Result.Failure<SaveReviewerEvaluationResponse>(normalized.Error);
        command = normalized.Value;
        var snapshot = await assignments.GetByIdAsync(command.AssignmentId, cancellationToken);
        if (snapshot is null) return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.AssignmentNotFound);
        if (!await authorization.CanAccessAsync(snapshot, true, cancellationToken))
            return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.AccessDenied);
        try
        {
            var result = await coordinator.ExecuteWithPostSaveAsync(
                token => SaveAsync(command, actor, snapshot, ReviewerEvaluationInput.Fingerprint(command), token),
                CompleteReceiptAsync, cancellationToken);
            if (result.Error is { } error)
            {
                return Result.Failure<SaveReviewerEvaluationResponse>(error);
            }

            await DefenseCouncilCacheKeys.InvalidateGlobalAsync(cache, cancellationToken);
            return Result.Success(new SaveReviewerEvaluationResponse(result.Response!, result.StatusCode));
        }
        catch (DefenseCouncilConcurrencyException) { return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.ConcurrencyConflict); }
        catch (DefenseCouncilUniqueConstraintException) { return Result.Failure<SaveReviewerEvaluationResponse>(ReviewerEvaluationErrors.ConcurrencyConflict); }
    }

    private async Task<ReviewerAssignmentTransactionResult<SaveResult>> SaveAsync(SaveReviewerEvaluationCommand command,
        Guid actor, ReviewerAssignment snapshot, string fingerprint, CancellationToken token)
    {
        await receipts.LockRequestAsync(actor, Operation, command.IdempotencyKey, token);
        // A request can wait behind another receipt writer; do not replay on a revoked account.
        if (!await authorization.CanAccessAsync(snapshot, true, token)) return Abort(ReviewerEvaluationErrors.AccessDenied);
        var prior = await receipts.GetAsync(actor, Operation, command.IdempotencyKey, token);
        if (prior is not null)
        {
            if (prior.RequestFingerprint != fingerprint) return Abort(ReviewerEvaluationErrors.IdempotencyConflict);
            var response = JsonSerializer.Deserialize<ReviewerEvaluationDto>(prior.ResponsePayloadJson)
                ?? throw new InvalidOperationException("The committed evaluation receipt is invalid.");
            return ReviewerAssignmentTransactionResult<SaveResult>.Abort(new(Response: response, StatusCode: prior.ResponseStatusCode));
        }
        if (snapshot.IsLegacy) return Abort(ReviewerEvaluationErrors.LegacyRecordNeedsResolution);
        var early = await evaluations.GetByAssignmentAsync(snapshot.Id, token);
        if (early?.IsLegacy == true) return Abort(ReviewerEvaluationErrors.LegacyRecordNeedsResolution);
        if (early?.IsLocked == true) return Abort(ReviewerEvaluationErrors.Locked);

        // The existing guard holds all owner locks and the parent assignment to commit.
        var access = await workflow.LockForEvaluationAsync(snapshot.Id, snapshot.ReviewerId, token);
        if (access is null) return Abort(ReviewerEvaluationErrors.NotEligible);
        if (access.AssignmentVersion != command.ExpectedAssignmentVersion) return Abort(ReviewerEvaluationErrors.AssignmentVersionConflict);
        var batch = await batches.GetAsync(access.Binding.BatchId, token);
        if (batch is null) return Abort(ReviewerEvaluationErrors.ContextInvalid);
        if (!batch.IsOpen) return Abort(ReviewerEvaluationErrors.BatchNotOpen);
        var assignment = await assignments.GetByIdAsync(snapshot.Id, token)
            ?? throw new InvalidOperationException("The locked assignment disappeared.");
        var evaluation = await evaluations.LockByAssignmentAsync(snapshot.Id, token);
        if (evaluation?.IsLegacy == true) return Abort(ReviewerEvaluationErrors.LegacyRecordNeedsResolution);
        if (evaluation?.IsLocked == true) return Abort(ReviewerEvaluationErrors.Locked);
        if (evaluation is null ? command.ExpectedEvaluationVersion.HasValue : command.ExpectedEvaluationVersion != evaluation.Version)
            return Abort(ReviewerEvaluationErrors.VersionConflict);

        decimal? scoreToApply = command.Score;
        List<EvaluationDetailResponseDto>? detailDtos = null;
        Dictionary<Guid, GradingCriteria>? criteriaMap = null;
        int totalCriteriaCount = 0;
        if (command.Details is { Count: > 0 } details)
        {
            var batchCriteria = await evaluations.GetGradingCriteriaByBatchIdAsync(access.Binding.BatchId, token);
            totalCriteriaCount = batchCriteria.Count;
            criteriaMap = batchCriteria.ToDictionary(c => c.Id);

            foreach (var item in details)
            {
                if (!criteriaMap.TryGetValue(item.CriteriaId, out var criterion))
                {
                    return Abort(ReviewerEvaluationErrors.CriteriaNotFound(item.CriteriaId));
                }
                if (item.Score < 0)
                {
                    return Abort(ReviewerEvaluationErrors.CriteriaScoreNegative(criterion.CriteriaName));
                }
                if (item.Score > criterion.MaxScore)
                {
                    return Abort(ReviewerEvaluationErrors.ScoreExceedsMaxScore(criterion.CriteriaName, item.Score, criterion.MaxScore));
                }
            }

            if (command.Action == ReviewerEvaluationAction.Submit)
            {
                var hasStudentScope = details.Any(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty);
                if (hasStudentScope)
                {
                    foreach (var studentGroup in details.GroupBy(d => d.StudentId!.Value))
                    {
                        var submittedIds = studentGroup.Select(d => d.CriteriaId).ToHashSet();
                        foreach (var req in batchCriteria)
                        {
                            if (!submittedIds.Contains(req.Id))
                            {
                                return Abort(ReviewerEvaluationErrors.MissingRequiredCriteria(req.CriteriaName));
                            }
                        }
                    }
                }
                else
                {
                    var submittedIds = details.Select(d => d.CriteriaId).ToHashSet();
                    foreach (var req in batchCriteria)
                    {
                        if (!submittedIds.Contains(req.Id))
                        {
                            return Abort(ReviewerEvaluationErrors.MissingRequiredCriteria(req.CriteriaName));
                        }
                    }
                }
            }

            scoreToApply = StudentEvaluationGroupingHelper.ComputeAggregateScore(details);
        }
        else if (command.Action == ReviewerEvaluationAction.Submit && scoreToApply is null)
        {
            return Abort(ReviewerEvaluationErrors.IncompleteEvaluation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var status = evaluation is null ? 201 : 200;
        if (evaluation is null)
        {
            evaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), assignment.Id, scoreToApply, command.Comment, command.DefenseApproval, actor, now);
            evaluations.Add(evaluation);
        }
        var applied = command.Action == ReviewerEvaluationAction.Submit
            ? evaluation.TrySubmit(scoreToApply, command.Comment, command.DefenseApproval, actor, now)
            : evaluation.TrySaveDraft(scoreToApply, command.Comment, command.DefenseApproval, actor, now);
        if (!applied) return Abort(ReviewerEvaluationErrors.Locked);

        if (command.Details is { Count: > 0 } detailsToSave)
        {
            var detailsList = detailsToSave.Select(d => new EvaluationDetail
            {
                Id = Guid.NewGuid(),
                CriteriaId = d.CriteriaId,
                EvaluationId = evaluation.Id,
                StudentId = d.StudentId,
                Score = d.Score
            }).ToList();
            await evaluations.SaveEvaluationDetailsAsync(evaluation.Id, detailsList, token);

            detailDtos = detailsList.Select(d =>
            {
                var crit = criteriaMap![d.CriteriaId];
                return new EvaluationDetailResponseDto(d.Id, d.CriteriaId, crit.CriteriaCode, crit.CriteriaName, d.Score, crit.MaxScore, d.StudentId);
            }).ToList();
        }

        var receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), actor, Operation, command.IdempotencyKey, fingerprint, status, now);
        receipts.Add(receipt);
        return ReviewerAssignmentTransactionResult<SaveResult>.Commit(new(Assignment: assignment, Evaluation: evaluation,
            Receipt: receipt, TopicId: access.Binding.TopicId, BatchId: access.Binding.BatchId, StatusCode: status, Details: detailDtos, TotalCriteriaCount: totalCriteriaCount));
    }

    private async Task<SaveResult> CompleteReceiptAsync(SaveResult result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        audit.AddSaved(result.Evaluation!, result.Assignment!);
        var response = ReviewerEvaluationDtoMapper.ToDto(result.Evaluation!, result.Assignment!, result.TopicId, result.BatchId, result.Details, result.TotalCriteriaCount);
        result.Receipt!.SetResponsePayload(JsonSerializer.Serialize(response));

        var evaluation = result.Evaluation!;
        var assignment = result.Assignment!;
        if (notificationOutbox is not null
            && evaluation.Status == ReviewerEvaluationStatus.Submitted
            && defenseGroupContract is not null)
        {
            var groupSnapshot = await defenseGroupContract.GetGroupByIdAsync(assignment.GroupId, token);
            if (groupSnapshot is not null)
            {
                var studentIds = groupSnapshot.Members
                    .Where(m => string.Equals(m.ConfirmationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.StudentId)
                    .Distinct()
                    .ToArray();

                string? topicTitle = null;
                if (topicSummaryReader is not null)
                {
                    var topics = await topicSummaryReader.GetByIdsAsync([result.TopicId], token);
                    if (topics.TryGetValue(result.TopicId, out var topicSummary))
                    {
                        topicTitle = topicSummary.TitleVi;
                    }
                }

                var shortTopic = DefenseCouncilNotificationFormatting.Shorten(topicTitle, 55);
                var occurredAtUtc = evaluation.SubmittedAtUtc ?? evaluation.EvaluatedAt;

                if (evaluation.DefenseApproval == DefenseApprovalStatus.Rejected)
                {
                    var metadata = new List<NotificationEmailMetaItem>
                    {
                        new("Giai đoạn dừng", "Đánh giá của GVPB — Không đạt ra Hội đồng")
                    };
                    if (!string.IsNullOrWhiteSpace(topicTitle))
                    {
                        metadata.Add(new NotificationEmailMetaItem("Đề tài", topicTitle.Trim()));
                    }
                    if (!string.IsNullOrWhiteSpace(evaluation.Comment))
                    {
                        metadata.Add(new NotificationEmailMetaItem("Nhận xét GVPB", evaluation.Comment.Trim()));
                    }

                    if (studentIds.Length > 0)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "GROUP_STOPPED",
                                Title: "Thông báo không đạt điều kiện ra Hội đồng",
                                Content: "GVPB đánh giá không đạt ra Hội đồng, nhóm bị dừng bảo vệ KLTN trong đợt này.",
                                RecipientUserIds: studentIds,
                                ResourceType: "StudentGroup",
                                ResourceId: assignment.GroupId,
                                AcademicBatchId: result.BatchId,
                                ActionPath: "/thesis/final-report",
                                Priority: "Critical",
                                EnqueuePush: true,
                                EnqueueEmail: true,
                                EmailSubject: "[UGTM] Thông báo kết quả phản biện (Không đạt ra Hội đồng)",
                                EmailCategoryLabel: "Đánh giá phản biện",
                                EmailCtaLabel: "Xem nhận xét phản biện",
                                EmailNotice: "Vui lòng liên hệ Văn phòng Khoa hoặc GVHD để được giải đáp chi tiết.",
                                EmailMetadata: metadata),
                            occurredAtUtc);
                    }

                    if (assignment.AdvisorIdAtAssignment is { } advisorStoppedId && advisorStoppedId != Guid.Empty)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "GROUP_STOPPED",
                                Title: "Nhóm hướng dẫn không đạt ra Hội đồng",
                                Content: $"GVPB đánh giá đề tài \"{shortTopic}\" không đạt điều kiện ra Hội đồng.",
                                RecipientUserIds: [advisorStoppedId],
                                ResourceType: "StudentGroup",
                                ResourceId: assignment.GroupId,
                                AcademicBatchId: result.BatchId,
                                ActionPath: "/topics/advising",
                                Priority: "Critical",
                                EnqueuePush: true,
                                EnqueueEmail: true,
                                EmailSubject: $"[UGTM] Nhóm không đạt ra Hội đồng: {shortTopic}",
                                EmailCategoryLabel: "Đánh giá phản biện",
                                EmailCtaLabel: "Xem nhóm hướng dẫn",
                                EmailMetadata: metadata),
                            occurredAtUtc);
                    }
                }
                else
                {
                    if (studentIds.Length > 0)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "REVIEWER_EVALUATION_SUBMITTED",
                                Title: "Đã có kết quả đánh giá phản biện",
                                Content: "GVPB đã phát hành nhận xét. Nhóm vui lòng xem góp ý và nộp bản sửa nếu cần.",
                                RecipientUserIds: studentIds,
                                ResourceType: "ReviewerEvaluation",
                                ResourceId: evaluation.Id,
                                AcademicBatchId: result.BatchId,
                                ActionPath: "/thesis/final-report",
                                Priority: "Normal",
                                EnqueuePush: true,
                                EnqueueEmail: false),
                            occurredAtUtc);
                    }

                    if (assignment.AdvisorIdAtAssignment is { } advisorId && advisorId != Guid.Empty)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "REVIEWER_EVALUATION_SUBMITTED",
                                Title: "GVPB đã phát hành nhận xét nhóm hướng dẫn",
                                Content: $"GVPB đã hoàn tất đánh giá đề tài \"{shortTopic}\". Thầy/cô có thể theo dõi trên hệ thống.",
                                RecipientUserIds: [advisorId],
                                ResourceType: "ReviewerEvaluation",
                                ResourceId: evaluation.Id,
                                AcademicBatchId: result.BatchId,
                                ActionPath: "/topics/advising",
                                Priority: "Normal",
                                EnqueuePush: true,
                                EnqueueEmail: false),
                            occurredAtUtc);
                    }
                }
            }
        }

        return result with { Response = response };
    }
    private static ReviewerAssignmentTransactionResult<SaveResult> Abort(Error error)
        => ReviewerAssignmentTransactionResult<SaveResult>.Abort(new(Error: error));
    private sealed record SaveResult(Error? Error = null, ReviewerEvaluationDto? Response = null, int StatusCode = 200,
        ReviewerAssignment? Assignment = null, ReviewerEvaluation? Evaluation = null,
        ReviewerEvaluationIdempotencyRecord? Receipt = null, Guid TopicId = default, Guid BatchId = default,
        IReadOnlyList<EvaluationDetailResponseDto>? Details = null, int TotalCriteriaCount = 0);
}

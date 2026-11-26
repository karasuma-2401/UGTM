using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.ThesisManagement.Contracts.DefenseCouncils;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.DecideAdvisorEvaluation;

/// <summary>
/// Records the advisor's decision and, for an approval, atomically makes the group available
/// to SCRUM-104 reviewer assignment. A rejected decision remains editable and can be replaced;
/// an approved decision is immutable.
/// </summary>
public sealed class DecideAdvisorEvaluationHandler(
    IAdvisorEvaluationRepository evaluationRepository,
    IAdvisorEvaluationIdempotencyRepository idempotencyRepository,
    IAdvisorEvaluationWorkflowContract thesisWorkflow,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IAdvisorWorkflowTransactionCoordinator transactionCoordinator,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null,
    IDefenseGroupContract? defenseGroupContract = null,
    ITopicSummaryReader? topicSummaryReader = null)
{
    private const string Operation = "AdvisorEvaluation.Decide";

    public async Task<Result<AdvisorEvaluationDto>> HandleAsync(
        DecideAdvisorEvaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId || actorId == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.AuthenticationRequired);
        }
        if (command.FinalReportId == Guid.Empty
            || command.ExpectedReportVersionNo < 1
            || !command.ExpectedEvaluationVersion.HasValue
            || !command.AllowsReviewerAssignment.HasValue
            || command.IdempotencyKey == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.InvalidRequest);
        }

        try
        {
            AdvisorEvaluation.ValidateDecisionInput(
                command.AllowsReviewerAssignment.Value, command.RejectionReason);
        }
        catch (ArgumentException)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.InvalidDecision);
        }

        var fingerprint = CreateFingerprint(command);
        try
        {
            var workflowResult = await transactionCoordinator.ExecuteWithPostSaveAsync(
                token => DecideAsync(command, actorId, fingerprint, token),
                CompleteIdempotencyAndAuditsAsync,
                cancellationToken);
            if (workflowResult.Error is not null)
            {
                return Result.Failure<AdvisorEvaluationDto>(workflowResult.Error);
            }

            await DefenseCouncilCacheKeys.InvalidateGlobalAsync(cache, cancellationToken);
            return Result.Success(workflowResult.Response ?? AdvisorEvaluationDtoMapper.ToDto(workflowResult.Evaluation!));
        }
        catch (DefenseCouncilConcurrencyException)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
        catch (DefenseCouncilUniqueConstraintException)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
    }

    private async Task<DecisionWorkflowResult> DecideAsync(
        DecideAdvisorEvaluationCommand command,
        Guid actorId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var context = await thesisWorkflow.GetFinalReportContextAsync(command.FinalReportId, cancellationToken);
        if (context is null)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.FinalReportNotFound);
        }
        if (!context.GroupExists)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.GroupNotFound);
        }
        if (!IsAdministrator(currentUser))
        {
            var assignment = await advisorAssignmentContract.GetByTopicIdAsync(context.TopicId, cancellationToken);
            if (assignment?.AdvisorId != actorId)
            {
                return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.AccessDenied);
            }
        }

        // The replay check intentionally precedes current-state validation. An approval moves the
        // group out of FinalReportSubmitted, but a client that lost its response must still see the
        // response of the already committed request without creating duplicate audit entries.
        var priorRequest = await idempotencyRepository.GetAsync(actorId, Operation, command.IdempotencyKey, cancellationToken);
        if (priorRequest is not null)
        {
            if (!string.Equals(priorRequest.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.IdempotencyKeyReused);
            }

            var savedResponse = JsonSerializer.Deserialize<AdvisorEvaluationDto>(priorRequest.ResponsePayloadJson);
            return savedResponse is null
                ? DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict)
                : DecisionWorkflowResult.Replayed(savedResponse);
        }

        // An approval changes the group to ReadyForReviewerAssignment. The immutable evaluation
        // remains the reason that a later decision is denied, regardless of that group transition.
        var evaluation = await evaluationRepository.GetByGroupIdAsync(context.GroupId, cancellationToken);
        if (evaluation?.IsLocked == true)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.Locked);
        }

        if (!context.IsInitialFinal || !context.IsUnderEvaluation)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidEvaluationState);
        }
        if (!context.IsGroupFinalReportSubmitted)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidGroupState);
        }
        if (context.CurrentVersionNo != command.ExpectedReportVersionNo)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
        if (context.SubmissionFileId is null || context.SubmissionChecksumSha256 is null)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.FileNotFound);
        }

        var evaluationError = ValidateEvaluation(evaluation, command, context);
        if (evaluationError is not null)
        {
            return DecisionWorkflowResult.Failure(evaluationError);
        }

        var lockedReport = await thesisWorkflow.GetLockedInitialFinalReportAsync(command.FinalReportId, cancellationToken);
        if (lockedReport is null)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
        if (evaluation!.FinalReportId != lockedReport.FinalReportId
            || evaluation.ReportVersionNo != lockedReport.ReportVersionNo
            || evaluation.SubmissionFileId != lockedReport.SubmissionFileId)
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.VersionConflict);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!evaluation.TryDecide(command.AllowsReviewerAssignment!.Value, command.RejectionReason, actorId, nowUtc))
        {
            return DecisionWorkflowResult.Failure(AdvisorEvaluationErrors.Locked);
        }

        if (command.AllowsReviewerAssignment.Value)
        {
            var readiness = await thesisWorkflow.TryMakeGroupReadyForReviewerAssignmentAsync(
                lockedReport.GroupId,
                lockedReport.FinalReportId,
                lockedReport.ReportVersionNo,
                cancellationToken);
            var readinessError = ToError(readiness.Outcome);
            if (readinessError is not null)
            {
                return DecisionWorkflowResult.Failure(readinessError);
            }
        }

        var idempotencyRecord = AdvisorEvaluationIdempotencyRecord.Create(
            Guid.NewGuid(), actorId, Operation, command.IdempotencyKey, fingerprint, nowUtc);
        idempotencyRepository.Add(idempotencyRecord);
        return DecisionWorkflowResult.Changed(evaluation, idempotencyRecord);
    }

    private async Task<DecisionWorkflowResult> CompleteIdempotencyAndAuditsAsync(
        DecisionWorkflowResult workflowResult,
        CancellationToken cancellationToken)
    {
        if (workflowResult.Error is not null || workflowResult.IdempotencyRecord is null)
        {
            return workflowResult;
        }

        var evaluation = workflowResult.Evaluation!;
        var response = AdvisorEvaluationDtoMapper.ToDto(evaluation);
        workflowResult.IdempotencyRecord.SetResponsePayload(JsonSerializer.Serialize(response));
        auditOutbox.Add(new AuditIntegrationEntry(
            "AdvisorEvaluation",
            evaluation.Id.ToString(),
            evaluation.AllowsReviewerAssignment == true
                ? IntegrationAuditActions.AdvisorEvaluationApproved
                : IntegrationAuditActions.AdvisorEvaluationRejected,
            "Succeeded",
            evaluation.AllowsReviewerAssignment == true ? null : "ReviewerAssignmentRejected",
            new
            {
                evaluation.GroupId,
                evaluation.FinalReportId,
                evaluation.ReportVersionNo,
                evaluation.SubmissionFileId,
                evaluation.Score,
                evaluation.AllowsReviewerAssignment,
                HasRejectionReason = !string.IsNullOrWhiteSpace(evaluation.RejectionReason),
                evaluation.DecisionBy,
                evaluation.DecisionAt,
                evaluation.Version
            }), evaluation.DecisionAt!.Value);
        thesisWorkflow.AddDecisionAudit(new AdvisorEvaluationDecisionAuditContractDto(
            evaluation.Id,
            evaluation.GroupId,
            evaluation.FinalReportId!.Value,
            evaluation.ReportVersionNo!.Value,
            evaluation.Version,
            evaluation.AllowsReviewerAssignment!.Value,
            !string.IsNullOrWhiteSpace(evaluation.RejectionReason),
            evaluation.DecisionBy!.Value,
            evaluation.DecisionAt!.Value));

        if (notificationOutbox is not null && defenseGroupContract is not null)
        {
            var groupSnapshot = await defenseGroupContract.GetGroupByIdAsync(evaluation.GroupId, cancellationToken);
            if (groupSnapshot is not null)
            {
                var studentIds = groupSnapshot.Members
                    .Where(m => string.Equals(m.ConfirmationStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.StudentId)
                    .Distinct()
                    .ToArray();

                string? topicTitle = null;
                Guid? advisorId = null;
                if (topicSummaryReader is not null)
                {
                    var topics = await topicSummaryReader.GetByIdsAsync([groupSnapshot.TopicId], cancellationToken);
                    if (topics.TryGetValue(groupSnapshot.TopicId, out var topicSummary))
                    {
                        topicTitle = topicSummary.TitleVi;
                        advisorId = topicSummary.AdvisorId;
                    }
                }

                var occurredAtUtc = evaluation.DecisionAt!.Value;
                if (evaluation.AllowsReviewerAssignment == true && studentIds.Length > 0)
                {
                    notificationOutbox.Enqueue(
                        new NotificationOutboxEntry(
                            NotificationType: "ADVISOR_EVALUATION_APPROVED",
                            Title: "GVHD đã duyệt chuyển phản biện",
                            Content: "GVHD đã đánh giá đạt và chuyển hồ sơ của nhóm sang bước phân công phản biện.",
                            RecipientUserIds: studentIds,
                            ResourceType: "AdvisorEvaluation",
                            ResourceId: evaluation.Id,
                            AcademicBatchId: groupSnapshot.BatchId,
                            ActionPath: "/thesis/final-report",
                            Priority: "Normal",
                            EnqueuePush: true,
                            EnqueueEmail: false),
                        occurredAtUtc);
                }
                else if (evaluation.AllowsReviewerAssignment == false)
                {
                    var metadata = new List<NotificationEmailMetaItem>
                    {
                        new("Giai đoạn dừng", "Đánh giá của GVHD — Không đạt chuyển phản biện")
                    };
                    if (!string.IsNullOrWhiteSpace(topicTitle))
                    {
                        metadata.Add(new NotificationEmailMetaItem("Đề tài", topicTitle.Trim()));
                    }
                    if (!string.IsNullOrWhiteSpace(evaluation.RejectionReason))
                    {
                        metadata.Add(new NotificationEmailMetaItem("Lý do / Nhận xét", evaluation.RejectionReason.Trim()));
                    }

                    if (studentIds.Length > 0)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "GROUP_STOPPED",
                                Title: "Thông báo không đạt điều kiện ra phản biện",
                                Content: "GVHD đánh giá không đạt chuyển phản biện, nhóm tạm dừng tiến trình ra Hội đồng.",
                                RecipientUserIds: studentIds,
                                ResourceType: "StudentGroup",
                                ResourceId: evaluation.GroupId,
                                AcademicBatchId: groupSnapshot.BatchId,
                                ActionPath: "/thesis/final-report",
                                Priority: "Critical",
                                EnqueuePush: true,
                                EnqueueEmail: true,
                                EmailSubject: "[UGTM] Thông báo kết quả đánh giá của GVHD (Không đạt phản biện)",
                                EmailCategoryLabel: "Đánh giá GVHD",
                                EmailCtaLabel: "Xem kết quả đánh giá",
                                EmailNotice: "Vui lòng trao đổi với GVHD hoặc Văn phòng Khoa để được hướng dẫn cụ thể.",
                                EmailMetadata: metadata),
                            occurredAtUtc);
                    }

                    if (advisorId.HasValue && advisorId.Value != Guid.Empty && advisorId.Value != evaluation.DecisionBy)
                    {
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "GROUP_STOPPED",
                                Title: "Nhóm hướng dẫn không đạt chuyển phản biện",
                                Content: $"Nhóm đề tài \"{DefenseCouncilNotificationFormatting.Shorten(topicTitle, 55)}\" không đạt điều kiện ra phản biện.",
                                RecipientUserIds: [advisorId.Value],
                                ResourceType: "StudentGroup",
                                ResourceId: evaluation.GroupId,
                                AcademicBatchId: groupSnapshot.BatchId,
                                ActionPath: "/topics/advising",
                                Priority: "Critical",
                                EnqueuePush: true,
                                EnqueueEmail: true,
                                EmailSubject: $"[UGTM] Nhóm không đạt chuyển phản biện: {DefenseCouncilNotificationFormatting.Shorten(topicTitle, 55)}",
                                EmailCategoryLabel: "Đánh giá GVHD",
                                EmailCtaLabel: "Xem nhóm hướng dẫn",
                                EmailMetadata: metadata),
                            occurredAtUtc);
                    }
                }
            }
        }

        return workflowResult with { Response = response };
    }

    private static Error? ValidateEvaluation(
        AdvisorEvaluation? evaluation,
        DecideAdvisorEvaluationCommand command,
        AdvisorEvaluationFinalReportContextContractDto context)
    {
        if (evaluation is null)
        {
            return AdvisorEvaluationErrors.NotFound;
        }
        if (evaluation.IsLegacy)
        {
            return AdvisorEvaluationErrors.LegacyRecordNeedsResolution;
        }
        if (evaluation.IsLocked)
        {
            return AdvisorEvaluationErrors.Locked;
        }
        if (evaluation.Version != command.ExpectedEvaluationVersion!.Value
            || evaluation.FinalReportId != context.FinalReportId
            || evaluation.ReportVersionNo != context.CurrentVersionNo
            || evaluation.SubmissionFileId != context.SubmissionFileId)
        {
            return AdvisorEvaluationErrors.VersionConflict;
        }

        return null;
    }

    private static Error? ToError(AdvisorEvaluationGroupReadinessOutcome outcome)
        => outcome switch
        {
            AdvisorEvaluationGroupReadinessOutcome.Succeeded => null,
            AdvisorEvaluationGroupReadinessOutcome.FinalReportNotFound => AdvisorEvaluationErrors.FinalReportNotFound,
            AdvisorEvaluationGroupReadinessOutcome.GroupNotFound => AdvisorEvaluationErrors.GroupNotFound,
            AdvisorEvaluationGroupReadinessOutcome.InvalidFinalReportState => AdvisorEvaluationErrors.InvalidEvaluationState,
            AdvisorEvaluationGroupReadinessOutcome.ReportVersionMismatch => AdvisorEvaluationErrors.ConcurrencyConflict,
            AdvisorEvaluationGroupReadinessOutcome.InvalidGroupState => AdvisorEvaluationErrors.InvalidGroupState,
            _ => AdvisorEvaluationErrors.ConcurrencyConflict
        };

    private static bool IsAdministrator(ICurrentUser user)
        => user.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));

    private static string CreateFingerprint(DecideAdvisorEvaluationCommand command)
    {
        var canonical = string.Join("\n",
            command.FinalReportId.ToString("N"),
            command.ExpectedReportVersionNo.ToString(CultureInfo.InvariantCulture),
            command.ExpectedEvaluationVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            command.AllowsReviewerAssignment?.ToString() ?? string.Empty,
            command.RejectionReason ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record DecisionWorkflowResult(
        AdvisorEvaluation? Evaluation,
        AdvisorEvaluationIdempotencyRecord? IdempotencyRecord,
        AdvisorEvaluationDto? Response,
        Error? Error)
    {
        public static DecisionWorkflowResult Changed(AdvisorEvaluation evaluation, AdvisorEvaluationIdempotencyRecord record)
            => new(evaluation, record, null, null);

        public static DecisionWorkflowResult Replayed(AdvisorEvaluationDto response)
            => new(null, null, response, null);

        public static DecisionWorkflowResult Failure(Error error)
            => new(null, null, null, error);
    }
}

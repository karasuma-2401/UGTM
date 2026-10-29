using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

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
    TimeProvider timeProvider)
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

            return Result.Success(workflowResult.Response ?? ToDto(workflowResult.Evaluation!));
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

    private Task<DecisionWorkflowResult> CompleteIdempotencyAndAuditsAsync(
        DecisionWorkflowResult workflowResult,
        CancellationToken cancellationToken)
    {
        if (workflowResult.Error is not null || workflowResult.IdempotencyRecord is null)
        {
            return Task.FromResult(workflowResult);
        }

        var evaluation = workflowResult.Evaluation!;
        var response = ToDto(evaluation);
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
                evaluation.RejectionReason,
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
            evaluation.RejectionReason,
            evaluation.DecisionBy!.Value,
            evaluation.DecisionAt!.Value));
        return Task.FromResult(workflowResult with { Response = response });
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

    private static AdvisorEvaluationDto ToDto(AdvisorEvaluation evaluation)
        => new(
            evaluation.Id,
            evaluation.GroupId,
            evaluation.FinalReportId!.Value,
            evaluation.ReportVersionNo!.Value,
            evaluation.SubmissionFileId!.Value,
            evaluation.SubmissionChecksumSha256!,
            evaluation.Score,
            evaluation.Comment!,
            evaluation.EvaluatedBy!.Value,
            evaluation.EvaluatedAt,
            evaluation.AllowsReviewerAssignment,
            evaluation.RejectionReason,
            evaluation.DecisionBy,
            evaluation.DecisionAt,
            evaluation.Version,
            evaluation.IsLocked);

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

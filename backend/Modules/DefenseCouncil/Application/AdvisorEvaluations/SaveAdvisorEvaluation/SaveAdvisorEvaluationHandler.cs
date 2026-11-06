using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.AdvisorEvaluations.SaveAdvisorEvaluation;

public sealed class SaveAdvisorEvaluationHandler(
    IAdvisorEvaluationRepository evaluationRepository,
    IAdvisorEvaluationIdempotencyRepository idempotencyRepository,
    IAdvisorEvaluationWorkflowContract thesisWorkflow,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IAdvisorWorkflowTransactionCoordinator transactionCoordinator,
    IDefenseCouncilAuditOutbox auditOutbox,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string Operation = "AdvisorEvaluation.Save";

    public async Task<Result<AdvisorEvaluationDto>> HandleAsync(
        SaveAdvisorEvaluationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actorId || actorId == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.AuthenticationRequired);
        }
        if (command.FinalReportId == Guid.Empty || command.ExpectedReportVersionNo < 1 || command.IdempotencyKey == Guid.Empty)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.InvalidRequest);
        }

        var fingerprint = CreateFingerprint(command);
        try
        {
            var workflowResult = await transactionCoordinator.ExecuteWithPostSaveAsync(
                token => SaveAsync(command, actorId, fingerprint, token),
                CompleteIdempotencyRecordAsync,
                cancellationToken);
            if (workflowResult.Error is not null)
            {
                return Result.Failure<AdvisorEvaluationDto>(workflowResult.Error);
            }
            return Result.Success(workflowResult.Response ?? AdvisorEvaluationDtoMapper.ToDto(workflowResult.Evaluation!, workflowResult.Details));
        }
        catch (DefenseCouncilConcurrencyException)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
        catch (DefenseCouncilUniqueConstraintException exception)
            when (string.Equals(exception.ConstraintName, "ix_advisor_evaluation_group_id", StringComparison.Ordinal))
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.AlreadyExists);
        }
        catch (DefenseCouncilUniqueConstraintException)
        {
            return Result.Failure<AdvisorEvaluationDto>(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
    }

    private async Task<SaveWorkflowResult> SaveAsync(
        SaveAdvisorEvaluationCommand command,
        Guid actorId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var context = await thesisWorkflow.GetFinalReportContextAsync(command.FinalReportId, cancellationToken);
        if (context is null)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.FinalReportNotFound);
        }
        if (!context.GroupExists)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.GroupNotFound);
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(context.TopicId, cancellationToken);
        if (!IsAdministrator(currentUser))
        {
            if (assignment?.AdvisorId != actorId)
            {
                return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.AccessDenied);
            }
        }

        var priorRequest = await idempotencyRepository.GetAsync(actorId, Operation, command.IdempotencyKey, cancellationToken);
        if (priorRequest is not null)
        {
            if (!string.Equals(priorRequest.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.IdempotencyKeyReused);
            }

            var savedResponse = JsonSerializer.Deserialize<AdvisorEvaluationDto>(priorRequest.ResponsePayloadJson);
            return savedResponse is null
                ? SaveWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict)
                : SaveWorkflowResult.Replayed(savedResponse);
        }

        // A successful approval moves the group out of FinalReportSubmitted. Check the aggregate
        // before that state guard so every later score-edit attempt consistently reports that the
        // evaluation itself is locked rather than suggesting the group can be made editable again.
        var existing = await evaluationRepository.GetByGroupIdAsync(context.GroupId, cancellationToken);
        if (existing?.IsLocked == true)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.Locked);
        }

        if (!context.IsInitialFinal || !context.IsUnderEvaluation)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidEvaluationState);
        }
        if (!context.IsGroupFinalReportSubmitted)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidGroupState);
        }
        if (context.CurrentVersionNo != command.ExpectedReportVersionNo)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict);
        }
        if (context.SubmissionFileId is null || context.SubmissionChecksumSha256 is null)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.FileNotFound);
        }

        var existingError = ValidateExistingEvaluation(existing, command.ExpectedEvaluationVersion, context);
        if (existingError is not null)
        {
            return SaveWorkflowResult.Failure(existingError);
        }

        decimal finalScore;
        Dictionary<Guid, GradingCriteria>? criteriaMap = null;
        if (command.Details is { Count: > 0 } details)
        {
            if (details.Select(d => d.CriteriaId).Distinct().Count() != details.Count)
            {
                return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.DuplicateCriteria);
            }

            var batchId = assignment?.BatchId ?? Guid.Empty;
            var batchCriteria = batchId != Guid.Empty
                ? await evaluationRepository.GetGradingCriteriaByBatchIdAsync(batchId, cancellationToken)
                : [];

            criteriaMap = batchCriteria.ToDictionary(c => c.Id);

            foreach (var item in details)
            {
                if (!criteriaMap.TryGetValue(item.CriteriaId, out var criterion))
                {
                    return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.CriteriaNotFound(item.CriteriaId));
                }

                if (item.Score < 0)
                {
                    return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.CriteriaScoreNegative(criterion.CriteriaName));
                }

                if (item.Score > criterion.MaxScore)
                {
                    return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.ScoreExceedsMaxScore(criterion.CriteriaName, item.Score, criterion.MaxScore));
                }
            }

            var submittedIds = details.Select(d => d.CriteriaId).ToHashSet();
            foreach (var req in batchCriteria)
            {
                if (!submittedIds.Contains(req.Id))
                {
                    return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.MissingRequiredCriteria(req.CriteriaName));
                }
            }

            finalScore = Math.Round(details.Sum(d => d.Score), 2);
        }
        else
        {
            if (!command.Score.HasValue)
            {
                return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidScore);
            }
            finalScore = command.Score.Value;
        }

        try
        {
            AdvisorEvaluation.ValidateEvaluationInput(finalScore, command.Comment);
        }
        catch (ArgumentOutOfRangeException exception) when (string.Equals(exception.ParamName, "score", StringComparison.Ordinal))
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidScore);
        }
        catch (ArgumentException)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.InvalidComment);
        }

        var lockedReport = await thesisWorkflow.GetLockedInitialFinalReportAsync(command.FinalReportId, cancellationToken);
        if (lockedReport is null)
        {
            return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.ConcurrencyConflict);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        AdvisorEvaluation evaluation;
        var created = existing is null;
        if (created)
        {
            evaluation = AdvisorEvaluation.Create(
                Guid.NewGuid(),
                lockedReport.GroupId,
                lockedReport.FinalReportId,
                lockedReport.ReportVersionNo,
                lockedReport.SubmissionFileId,
                lockedReport.SubmissionChecksumSha256,
                finalScore,
                command.Comment!,
                actorId,
                nowUtc);
            evaluationRepository.Add(evaluation);
        }
        else
        {
            evaluation = existing!;
            if (!evaluation.TryUpdate(finalScore, command.Comment!, actorId, nowUtc))
            {
                return SaveWorkflowResult.Failure(AdvisorEvaluationErrors.Locked);
            }
        }

        List<EvaluationDetailResponseDto>? detailDtos = null;
        if (command.Details is { Count: > 0 } detailsToSave)
        {
            var detailsList = detailsToSave.Select(d => new EvaluationDetail
            {
                Id = Guid.NewGuid(),
                CriteriaId = d.CriteriaId,
                EvaluationId = evaluation.Id,
                Score = d.Score
            }).ToList();

            await evaluationRepository.SaveEvaluationDetailsAsync(evaluation.Id, detailsList, cancellationToken);

            detailDtos = detailsToSave.Select(d =>
            {
                var crit = criteriaMap![d.CriteriaId];
                return new EvaluationDetailResponseDto(Guid.Empty, d.CriteriaId, crit.CriteriaCode, crit.CriteriaName, d.Score, crit.MaxScore);
            }).ToList();
        }

        var idempotencyRecord = AdvisorEvaluationIdempotencyRecord.Create(
            Guid.NewGuid(), actorId, Operation, command.IdempotencyKey, fingerprint, nowUtc);
        idempotencyRepository.Add(idempotencyRecord);
        auditOutbox.Add(new AuditIntegrationEntry(
            "AdvisorEvaluation", evaluation.Id.ToString(), IntegrationAuditActions.AdvisorEvaluationSaved,
            "Succeeded", null, new
            {
                evaluation.GroupId,
                evaluation.FinalReportId,
                evaluation.ReportVersionNo,
                evaluation.SubmissionFileId,
                Operation = created ? "Created" : "Updated"
            }), nowUtc);
        return SaveWorkflowResult.Changed(evaluation, idempotencyRecord, detailDtos);
    }

    private static Task<SaveWorkflowResult> CompleteIdempotencyRecordAsync(
        SaveWorkflowResult workflowResult,
        CancellationToken cancellationToken)
    {
        if (workflowResult.Error is not null || workflowResult.IdempotencyRecord is null)
        {
            return Task.FromResult(workflowResult);
        }

        workflowResult.IdempotencyRecord.SetResponsePayload(
            JsonSerializer.Serialize(AdvisorEvaluationDtoMapper.ToDto(workflowResult.Evaluation!, workflowResult.Details)));
        return Task.FromResult(workflowResult);
    }

    private static Error? ValidateExistingEvaluation(
        AdvisorEvaluation? evaluation,
        uint? expectedVersion,
        AdvisorEvaluationFinalReportContextContractDto context)
    {
        if (evaluation is null)
        {
            return expectedVersion.HasValue ? AdvisorEvaluationErrors.NotFound : null;
        }
        if (evaluation.IsLegacy)
        {
            return AdvisorEvaluationErrors.LegacyRecordNeedsResolution;
        }
        if (evaluation.IsLocked)
        {
            return AdvisorEvaluationErrors.Locked;
        }
        if (!expectedVersion.HasValue)
        {
            return AdvisorEvaluationErrors.AlreadyExists;
        }
        if (evaluation.Version != expectedVersion.Value
            || evaluation.FinalReportId != context.FinalReportId
            || evaluation.ReportVersionNo != context.CurrentVersionNo
            || evaluation.SubmissionFileId != context.SubmissionFileId)
        {
            return AdvisorEvaluationErrors.VersionConflict;
        }

        return null;
    }

    private static bool IsAdministrator(ICurrentUser user)
        => user.Roles.Any(role => string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));

    private static string CreateFingerprint(SaveAdvisorEvaluationCommand command)
    {
        var detailsCanonical = command.Details != null
            ? string.Join(";", command.Details.OrderBy(d => d.CriteriaId).Select(d => $"{d.CriteriaId:N}:{d.Score.ToString("G29", CultureInfo.InvariantCulture)}"))
            : string.Empty;
        var canonical = string.Join("\n",
            command.FinalReportId.ToString("N"),
            command.ExpectedReportVersionNo.ToString(CultureInfo.InvariantCulture),
            command.ExpectedEvaluationVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            command.Score?.ToString("G29", CultureInfo.InvariantCulture) ?? string.Empty,
            command.Comment ?? string.Empty,
            detailsCanonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private sealed record SaveWorkflowResult(
        AdvisorEvaluation? Evaluation,
        AdvisorEvaluationIdempotencyRecord? IdempotencyRecord,
        AdvisorEvaluationDto? Response,
        Error? Error,
        IReadOnlyList<EvaluationDetailResponseDto>? Details = null)
    {
        public static SaveWorkflowResult Changed(AdvisorEvaluation evaluation, AdvisorEvaluationIdempotencyRecord record, IReadOnlyList<EvaluationDetailResponseDto>? details = null)
            => new(evaluation, record, null, null, details);
        public static SaveWorkflowResult Replayed(AdvisorEvaluationDto response)
            => new(null, null, response, null, null);
        public static SaveWorkflowResult Failure(Error error)
            => new(null, null, null, error, null);
    }
}

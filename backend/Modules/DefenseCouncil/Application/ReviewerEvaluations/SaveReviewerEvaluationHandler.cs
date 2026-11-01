using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class SaveReviewerEvaluationHandler(
    IReviewerAssignmentWorkflowContract workflow, IReviewerAssignmentTransactionCoordinator coordinator,
    IReviewerAssignmentRepository assignments, IReviewerEvaluationRepository evaluations,
    IReviewerEvaluationIdempotencyRepository receipts, IReviewerAssignmentBatchReader batches,
    ReviewerEvaluationAuthorization authorization, ReviewerEvaluationAudit audit, ICurrentUser currentUser, TimeProvider timeProvider)
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
            return result.Error is { } error ? Result.Failure<SaveReviewerEvaluationResponse>(error)
                : Result.Success(new SaveReviewerEvaluationResponse(result.Response!, result.StatusCode));
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var status = evaluation is null ? 201 : 200;
        if (evaluation is null)
        {
            evaluation = ReviewerEvaluation.CreateDraft(Guid.NewGuid(), assignment.Id, command.Score, command.Comment, command.DefenseApproval, actor, now);
            evaluations.Add(evaluation);
        }
        var applied = command.Action == ReviewerEvaluationAction.Submit
            ? evaluation.TrySubmit(command.Score, command.Comment, command.DefenseApproval, actor, now)
            : evaluation.TrySaveDraft(command.Score, command.Comment, command.DefenseApproval, actor, now);
        if (!applied) return Abort(ReviewerEvaluationErrors.Locked);
        var receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), actor, Operation, command.IdempotencyKey, fingerprint, status, now);
        receipts.Add(receipt);
        return ReviewerAssignmentTransactionResult<SaveResult>.Commit(new(Assignment: assignment, Evaluation: evaluation,
            Receipt: receipt, TopicId: access.Binding.TopicId, BatchId: access.Binding.BatchId, StatusCode: status));
    }

    private Task<SaveResult> CompleteReceiptAsync(SaveResult result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        audit.AddSaved(result.Evaluation!, result.Assignment!);
        var response = ReviewerEvaluationDtoMapper.ToDto(result.Evaluation!, result.Assignment!, result.TopicId, result.BatchId);
        result.Receipt!.SetResponsePayload(JsonSerializer.Serialize(response));
        return Task.FromResult(result with { Response = response });
    }
    private static ReviewerAssignmentTransactionResult<SaveResult> Abort(Error error)
        => ReviewerAssignmentTransactionResult<SaveResult>.Abort(new(Error: error));
    private sealed record SaveResult(Error? Error = null, ReviewerEvaluationDto? Response = null, int StatusCode = 200,
        ReviewerAssignment? Assignment = null, ReviewerEvaluation? Evaluation = null,
        ReviewerEvaluationIdempotencyRecord? Receipt = null, Guid TopicId = default, Guid BatchId = default);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed record CancelReviewerAssignmentCommand(Guid AssignmentId, uint ExpectedAssignmentVersion,
    string? Reason, Guid IdempotencyKey);

public sealed class CancelReviewerAssignmentHandler(
    IReviewerAssignmentTransactionCoordinator coordinator,
    IReviewerAssignmentRepository assignments,
    IReviewerAssignmentIdempotencyRepository receipts,
    IReviewerAssignmentThesisReader thesis,
    IReviewerAssignmentTopicReader topics,
    IAdvisorNameReader names,
    ReviewerAssignmentAudit audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string Operation = "ReviewerAssignment.Cancel";

    public async Task<Result<ReviewerAssignmentDto>> HandleAsync(CancelReviewerAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.AuthenticationRequired);
        if (!ReviewerAssignmentAuthorization.CanManage(currentUser.Roles, currentUser.Permissions))
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.AccessDenied);
        if (command.AssignmentId == Guid.Empty || command.ExpectedAssignmentVersion == 0 || command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.InvalidRequest);
        var reason = command.Reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.EnumerateRunes().Count() > ReviewerAssignment.CancellationReasonMaximumLength)
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.InvalidCancellationReason);

        var canonical = $"{command.AssignmentId:N}:{command.ExpectedAssignmentVersion.ToString(CultureInfo.InvariantCulture)}:{reason}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        try
        {
            var result = await coordinator.ExecuteWithPostSaveAsync(
                token => CancelAsync(command, actor, reason, fingerprint, token), CompleteReceiptAsync, cancellationToken);
            return result.Error is { } error ? Result.Failure<ReviewerAssignmentDto>(error) : Result.Success(result.Response!);
        }
        catch (DefenseCouncilConcurrencyException)
        {
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.ConcurrencyConflict);
        }
        catch (DefenseCouncilUniqueConstraintException)
        {
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.ConcurrencyConflict);
        }
    }

    private async Task<ReviewerAssignmentTransactionResult<CancellationResult>> CancelAsync(
        CancelReviewerAssignmentCommand command, Guid actor, string reason, string fingerprint, CancellationToken token)
    {
        await receipts.LockRequestAsync(actor, Operation, command.IdempotencyKey, token);
        var prior = await receipts.GetAsync(actor, Operation, command.IdempotencyKey, token);
        if (prior is not null)
        {
            if (!string.Equals(prior.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Abort(ReviewerAssignmentErrors.IdempotencyKeyReused);
            var response = JsonSerializer.Deserialize<ReviewerAssignmentDto>(prior.ResponsePayloadJson)
                ?? throw new InvalidOperationException("The committed reviewer cancellation receipt is invalid.");
            return ReviewerAssignmentTransactionResult<CancellationResult>.Abort(new(Response: response));
        }

        // Evaluation writers take this same parent lock before checking active status and inserting a sheet.
        var assignment = await assignments.LockByIdAsync(command.AssignmentId, token);
        if (assignment is null) return Abort(ReviewerAssignmentErrors.NotFound);
        if (!assignment.IsActive) return Abort(ReviewerAssignmentErrors.AlreadyCancelled);
        if (assignment.Version != command.ExpectedAssignmentVersion) return Abort(ReviewerAssignmentErrors.VersionConflict);
        if (await assignments.HasEvaluationAsync(assignment.Id, token)) return Abort(ReviewerAssignmentErrors.EvaluationExists);

        // Response projections only: cancellation deliberately does not acquire creation eligibility locks
        // or require the batch, group state, report binding or reviewer account to remain eligible.
        var context = await thesis.GetAsync(assignment.GroupId, token);
        if (context is null) return Abort(ReviewerAssignmentErrors.ContextInvalid);
        var topic = await topics.GetAsync(context.TopicId, token);
        if (topic is null) return Abort(ReviewerAssignmentErrors.ContextInvalid);
        var reviewerNames = await names.GetNamesByIdsAsync([assignment.ReviewerId], token);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (now < assignment.AssignedAt) return Abort(ReviewerAssignmentErrors.ConcurrencyConflict);
        if (!assignment.TryCancel(actor, now, reason, false)) return Abort(ReviewerAssignmentErrors.ConcurrencyConflict);
        var receipt = ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), actor, Operation,
            command.IdempotencyKey, fingerprint, 200, now);
        receipts.Add(receipt);
        audit.AddCancelled(assignment);
        return ReviewerAssignmentTransactionResult<CancellationResult>.Commit(new(Assignment: assignment, Receipt: receipt,
            Topic: topic, ReviewerName: reviewerNames.GetValueOrDefault(assignment.ReviewerId)));
    }

    private static Task<CancellationResult> CompleteReceiptAsync(CancellationResult result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var response = ReviewerAssignmentDtoMapper.ToDto(result.Assignment!, result.Topic!, result.ReviewerName);
        result.Receipt!.SetResponsePayload(JsonSerializer.Serialize(response));
        return Task.FromResult(result with { Response = response });
    }

    private static ReviewerAssignmentTransactionResult<CancellationResult> Abort(Error error)
        => ReviewerAssignmentTransactionResult<CancellationResult>.Abort(new(Error: error));

    private sealed record CancellationResult(Error? Error = null, ReviewerAssignmentDto? Response = null,
        ReviewerAssignment? Assignment = null, ReviewerAssignmentIdempotencyRecord? Receipt = null,
        ReviewerAssignmentTopicContractDto? Topic = null, string? ReviewerName = null);
}

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
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed record CreateReviewerAssignmentCommand(Guid GroupId, Guid ReviewerId,
    Guid ExpectedAdvisorEvaluationId, int ExpectedReportVersionNo, Guid IdempotencyKey);

public sealed class CreateReviewerAssignmentHandler(
    IReviewerAssignmentWorkflowContract workflow,
    IReviewerAssignmentTransactionCoordinator coordinator,
    IReviewerAssignmentRepository assignments,
    IReviewerAssignmentIdempotencyRepository receipts,
    IReviewerAssignmentTopicReader topics,
    IAdvisorNameReader names,
    ReviewerAssignmentAudit audit,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string Operation = "ReviewerAssignment.Create";

    public async Task<Result<ReviewerAssignmentDto>> HandleAsync(CreateReviewerAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } actor || actor == Guid.Empty)
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.AuthenticationRequired);
        if (!ReviewerAssignmentAuthorization.CanManage(currentUser.Roles, currentUser.Permissions))
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.AccessDenied);
        if (command.GroupId == Guid.Empty || command.ReviewerId == Guid.Empty || command.ExpectedAdvisorEvaluationId == Guid.Empty
            || command.ExpectedReportVersionNo < 1 || command.IdempotencyKey == Guid.Empty)
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.InvalidRequest);

        var canonical = $"{command.GroupId:N}:{command.ReviewerId:N}:{command.ExpectedAdvisorEvaluationId:N}:"
            + command.ExpectedReportVersionNo.ToString(CultureInfo.InvariantCulture);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        try
        {
            var result = await coordinator.ExecuteWithPostSaveAsync(
                token => CreateAsync(command, actor, fingerprint, token), CompleteReceiptAsync, cancellationToken);
            return result.Error is { } error ? Result.Failure<ReviewerAssignmentDto>(error)
                : Result.Success(result.Response!);
        }
        catch (DefenseCouncilConcurrencyException)
        {
            return Result.Failure<ReviewerAssignmentDto>(ReviewerAssignmentErrors.ConcurrencyConflict);
        }
        catch (DefenseCouncilUniqueConstraintException exception)
        {
            return Result.Failure<ReviewerAssignmentDto>(exception.ConstraintName == "ux_reviewer_assignment_active_group"
                ? ReviewerAssignmentErrors.AlreadyAssigned : ReviewerAssignmentErrors.ConcurrencyConflict);
        }
    }

    private async Task<ReviewerAssignmentTransactionResult<CreationResult>> CreateAsync(
        CreateReviewerAssignmentCommand command, Guid actor, string fingerprint, CancellationToken token)
    {
        // Always acquire the request lock before any resource lock; a retry observes the committed receipt.
        await receipts.LockRequestAsync(actor, Operation, command.IdempotencyKey, token);
        var prior = await receipts.GetAsync(actor, Operation, command.IdempotencyKey, token);
        if (prior is not null)
        {
            if (!string.Equals(prior.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Abort(ReviewerAssignmentErrors.IdempotencyKeyReused);
            var response = JsonSerializer.Deserialize<ReviewerAssignmentDto>(prior.ResponsePayloadJson)
                ?? throw new InvalidOperationException("The committed reviewer assignment receipt is invalid.");
            return ReviewerAssignmentTransactionResult<CreationResult>.Abort(new(Response: response));
        }

        var eligibility = await workflow.LockForCreationAsync(command.GroupId, command.ReviewerId,
            command.ExpectedAdvisorEvaluationId, command.ExpectedReportVersionNo, token);
        if (eligibility.Outcome != ReviewerAssignmentEligibilityOutcome.Succeeded)
            return Abort(ReviewerAssignmentErrors.FromOutcome(eligibility.Outcome));
        var binding = eligibility.Binding!;
        // The topic is already locked by the guard; these values are response projections only.
        var topic = await topics.GetAsync(binding.TopicId, token)
            ?? throw new InvalidOperationException("The locked topic is missing.");
        var reviewerNames = await names.GetNamesByIdsAsync([command.ReviewerId], token);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var assignment = ReviewerAssignment.Create(Guid.NewGuid(), command.GroupId, command.ReviewerId,
            binding.AdvisorId, binding.AdvisorEvaluationId, binding.FinalReportId, binding.ReportVersionNo,
            binding.SubmissionFileId, binding.SubmissionChecksumSha256, actor, now);
        var receipt = ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), actor, Operation,
            command.IdempotencyKey, fingerprint, 201, now);
        assignments.Add(assignment);
        receipts.Add(receipt);
        audit.AddCreated(assignment);
        return ReviewerAssignmentTransactionResult<CreationResult>.Commit(new(Assignment: assignment, Receipt: receipt,
            Binding: binding, Topic: topic, ReviewerName: reviewerNames.GetValueOrDefault(command.ReviewerId)));
    }

    private static Task<CreationResult> CompleteReceiptAsync(CreationResult result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var response = ReviewerAssignmentDtoMapper.ToDto(result.Assignment!, result.Binding!, result.Topic!, result.ReviewerName);
        result.Receipt!.SetResponsePayload(JsonSerializer.Serialize(response));
        return Task.FromResult(result with { Response = response });
    }

    private static ReviewerAssignmentTransactionResult<CreationResult> Abort(Error error)
        => ReviewerAssignmentTransactionResult<CreationResult>.Abort(new(Error: error));

    private sealed record CreationResult(Error? Error = null, ReviewerAssignmentDto? Response = null,
        ReviewerAssignment? Assignment = null, ReviewerAssignmentIdempotencyRecord? Receipt = null,
        ReviewerAssignmentBindingContractDto? Binding = null, ReviewerAssignmentTopicContractDto? Topic = null,
        string? ReviewerName = null);
}

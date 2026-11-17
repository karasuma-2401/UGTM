using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAdvisor;

public sealed class ReviewRenameRequestByAdvisorHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicRenameContract topicRenameContract,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByAdvisorCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        var isApprove = string.Equals(command.Decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(command.Decision, "Reject", StringComparison.OrdinalIgnoreCase);

        if (!isApprove && !isReject)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidDecision);
        }

        if (isReject && string.IsNullOrWhiteSpace(command.RejectionReason))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.RejectionReasonRequired);
        }

        var request = await renameRequestRepository.GetByIdAsync(command.RequestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.RequestNotFound);
        }

        if (request.Status != RenameRequestStatus.Created)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var group = await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.GroupNotFound);
        }

        var topicSnapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
        if (topicSnapshot is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.TopicNotFound);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestAdvisor(currentUser, topicSnapshot.AdvisorId))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (isApprove)
        {
            request.ApproveByAdvisor(nowUtc);
        }
        else
        {
            request.RejectByAdvisor(command.RejectionReason!.Trim(), currentUser.UserId.Value, nowUtc);
        }

        var saveError = await unitOfWork.TrySaveChangesAsync(
            RenameRequestErrors.ConcurrencyConflict,
            RenameRequestErrors.ConcurrencyConflict,
            cancellationToken);

        if (saveError is not null)
        {
            return Result.Failure<RenameRequestDto>(saveError);
        }

        return Result.Success(RenameRequestMapper.ToDto(request));
    }
}

using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByFaculty;

public sealed class ReviewRenameRequestByFacultyHandler(
    IRenameRequestRepository renameRequestRepository,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByFacultyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestFaculty(currentUser))
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

        if (request.Status != RenameRequestStatus.AdvisorConfirmed)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (isApprove)
        {
            request.ApproveByFaculty(nowUtc);
        }
        else
        {
            request.RejectByFaculty(command.RejectionReason!.Trim(), currentUser.UserId.Value, nowUtc);
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

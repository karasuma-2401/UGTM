using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.ReviewByAcademicAffairs;

public sealed class ReviewRenameRequestByAcademicAffairsHandler(
    IRenameRequestRepository renameRequestRepository,
    IStudentGroupRepository groupRepository,
    ITopicRenameContract topicRenameContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        ReviewRenameRequestByAcademicAffairsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (!ThesisAuthorization.CanReviewRenameRequestAcademicAffairs(currentUser))
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

        if (request.Status != RenameRequestStatus.FacultyApproved)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidReviewState);
        }

        var group = await groupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.GroupNotFound);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (isApprove)
        {
            var updateResult = await topicRenameContract.UpdateTopicTitlesAsync(
                group.TopicId,
                request.NewTitleVi,
                request.NewTitleEn,
                cancellationToken);

            if (!updateResult.IsSuccess)
            {
                return Result.Failure<RenameRequestDto>(
                    Error.Conflict(
                        updateResult.ErrorCode ?? "RenameRequest.TopicUpdateFailed",
                        updateResult.ErrorMessage ?? "Không thể cập nhật tên đề tài."));
            }

            request.ApproveByAcademicAffairs(nowUtc);
            group.EnableSyllabusResubmission();

            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "RenameRequest",
                    request.Id.ToString(),
                    IntegrationAuditActions.RenameRequestApproved,
                    "Succeeded",
                    null,
                    new
                    {
                        request.GroupId,
                        group.TopicId,
                        OldTitleVi = request.OldTitleVi,
                        NewTitleVi = request.NewTitleVi,
                        OldTitleEn = request.OldTitleEn,
                        NewTitleEn = request.NewTitleEn,
                        ApprovedBy = currentUser.UserId.Value
                    }),
                nowUtc);
        }
        else
        {
            request.RejectByAcademicAffairs(command.RejectionReason!.Trim(), currentUser.UserId.Value, nowUtc);
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

using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.RenameRequests.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.RenameRequests;

namespace UGTM.Modules.ThesisManagement.Application.RenameRequests.CreateRenameRequest;

public sealed class CreateRenameRequestHandler(
    IStudentGroupRepository groupRepository,
    IRenameRequestRepository renameRequestRepository,
    IKltnBatchReader batchReader,
    ITopicRenameContract topicRenameContract,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<RenameRequestDto>> HandleAsync(
        CreateRenameRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.NewTitleVi)
            || string.IsNullOrWhiteSpace(command.Reason)
            || command.NewTitleVi.Trim().Length > 300
            || (command.NewTitleEn is not null && command.NewTitleEn.Trim().Length > 350))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.AccessDenied);
        }

        if (group.Status is not (StudentGroupStatus.InProgress or StudentGroupStatus.MidtermSubmitted))
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.InvalidGroupState);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var window = await batchReader.GetRenameRequestWindowAsync(membership.BatchId, nowUtc, cancellationToken);
        if (window is null || !window.IsRenameWindowOpen)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.OutsideAllowedWindow);
        }

        var hasActiveRequest = await renameRequestRepository.HasActiveRequestForGroupAsync(command.GroupId, cancellationToken);
        if (hasActiveRequest)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.ActiveRequestAlreadyExists);
        }

        var topicSnapshot = await topicRenameContract.GetTopicSnapshotAsync(group.TopicId, cancellationToken);
        if (topicSnapshot is null)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.TopicNotFound);
        }

        var trimmedNewTitleVi = command.NewTitleVi.Trim();
        var trimmedNewTitleEn = string.IsNullOrWhiteSpace(command.NewTitleEn) ? null : command.NewTitleEn.Trim();

        var isViSame = string.Equals(trimmedNewTitleVi, topicSnapshot.TitleVi, StringComparison.OrdinalIgnoreCase);
        var isEnSame = string.Equals(trimmedNewTitleEn ?? string.Empty, topicSnapshot.TitleEn ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        if (isViSame && isEnSame)
        {
            return Result.Failure<RenameRequestDto>(RenameRequestErrors.TitleUnchanged);
        }

        var request = RenameRequest.Create(
            Guid.NewGuid(),
            group.Id,
            currentUser.UserId.Value,
            topicSnapshot.TitleVi,
            trimmedNewTitleVi,
            topicSnapshot.TitleEn,
            trimmedNewTitleEn,
            command.Reason.Trim(),
            nowUtc);

        renameRequestRepository.Add(request);

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

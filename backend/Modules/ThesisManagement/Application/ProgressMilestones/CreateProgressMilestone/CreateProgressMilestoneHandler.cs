using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressMilestones.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressMilestones.CreateProgressMilestone;

public sealed class CreateProgressMilestoneHandler(
    IStudentGroupRepository groupRepository,
    IProgressMilestoneRepository milestoneRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ProgressMilestoneDto>> HandleAsync(
        CreateProgressMilestoneCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressMilestoneDto>(ProgressMilestoneErrors.AccessDenied);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.Title)
            || command.Title.Trim().Length > 250
            || command.DeadlineUtc.Kind != DateTimeKind.Utc
            || command.DeadlineUtc <= occurredAt)
        {
            return Result.Failure<ProgressMilestoneDto>(ProgressMilestoneErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<ProgressMilestoneDto>(ProgressMilestoneErrors.GroupNotFound);
        }

        if (group.Status != StudentGroupStatus.InProgress)
        {
            return Result.Failure<ProgressMilestoneDto>(ProgressMilestoneErrors.GateOneNotApproved);
        }

        if (!await IsAssignedAdvisorOrAdministratorAsync(group.TopicId, cancellationToken))
        {
            return Result.Failure<ProgressMilestoneDto>(ProgressMilestoneErrors.AccessDenied);
        }

        var milestone = ProgressMilestone.Create(
            Guid.NewGuid(),
            group.Id,
            command.Title,
            command.Description,
            command.DeadlineUtc,
            currentUser.UserId.Value,
            occurredAt);
        milestoneRepository.Add(milestone);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "ProgressMilestone",
                milestone.Id.ToString(),
                IntegrationAuditActions.ProgressMilestoneCreated,
                "Succeeded",
                null,
                new
                {
                    milestone.GroupId,
                    milestone.Title,
                    DeadlineUtc = milestone.Deadline,
                    milestone.CreatedBy
                }),
            occurredAt);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            ProgressMilestoneErrors.ConcurrencyConflict,
            cancellationToken: cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ProgressMilestoneDto>(saveError);
        }

        return Result.Success(ProgressMilestoneMapper.ToDto(milestone));
    }

    private async Task<bool> IsAssignedAdvisorOrAdministratorAsync(
        Guid topicId,
        CancellationToken cancellationToken)
    {
        if (ThesisAuthorization.IsAdministrator(currentUser))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(topicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId!.Value;
    }
}

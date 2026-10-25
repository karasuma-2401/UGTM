using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.TopicCatalog.Application.Common;
using UGTM.Modules.TopicCatalog.Application.Persistence;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;
using UGTM.Modules.TopicCatalog.Domain.Repositories;

namespace UGTM.Modules.TopicCatalog.Application.Topics.CreateTopic;

public sealed class CreateTopicHandler(
    ITopicRepository topicRepository,
    ITopicUnitOfWork unitOfWork,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser,
    IThesisRegistrationContract? registrationContract = null)
{
    private const int MaxTitleViLength = 300;
    private const int MaxTitleEnLength = 350;

    public async Task<Result<TopicSummaryDto>> HandleAsync(
        CreateTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        var isAdmin = HasRole(currentUser, "Admin");
        var isLecturer = HasRole(currentUser, "Lecturer");

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidBatchId);
        }

        if (string.IsNullOrWhiteSpace(command.TitleVi))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleVi);
        }

        var trimmedTitleVi = command.TitleVi.Trim();
        if (trimmedTitleVi.Length > MaxTitleViLength)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleVi);
        }

        string? trimmedTitleEn = null;
        if (!string.IsNullOrWhiteSpace(command.TitleEn))
        {
            trimmedTitleEn = command.TitleEn.Trim();
            if (trimmedTitleEn.Length > MaxTitleEnLength)
            {
                return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidTitleEn);
            }
        }

        if (command.StudentNumber is not (1 or 2))
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.InvalidStudentNumber);
        }

        var effectiveAdvisorId = isAdmin && command.AdvisorId.HasValue && command.AdvisorId.Value != Guid.Empty
            ? command.AdvisorId.Value
            : currentUser.UserId.Value;

        var batchExists = await batchReader.ExistsAsync(command.BatchId, cancellationToken);
        if (!batchExists)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.BatchNotFound);
        }

        var isDuplicate = await topicRepository.ExistsByAdvisorAndTitleAsync(
            command.BatchId,
            effectiveAdvisorId,
            trimmedTitleVi,
            excludeTopicId: null,
            cancellationToken);

        if (isDuplicate)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.DuplicateTitle);
        }

        var assignmentInputs = new List<AdvisorStudentAssignmentInputDto>();
        if (command.StudentIds is { Count: > 0 })
        {
            foreach (var sId in command.StudentIds)
            {
                assignmentInputs.Add(new AdvisorStudentAssignmentInputDto(StudentId: sId));
            }
        }

        if (command.StudentCodes is { Count: > 0 })
        {
            foreach (var code in command.StudentCodes)
            {
                assignmentInputs.Add(new AdvisorStudentAssignmentInputDto(UserCode: code));
            }
        }

        if (assignmentInputs.Count > command.StudentNumber.Value)
        {
            return Result.Failure<TopicSummaryDto>(TopicErrors.QuotaLessThanRegisteredStudents);
        }

        var topic = new Topic
        {
            Id = Guid.NewGuid(),
            BatchId = command.BatchId,
            AdvisorId = effectiveAdvisorId,
            TitleVi = trimmedTitleVi,
            TitleEn = trimmedTitleEn,
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            Requirements = string.IsNullOrWhiteSpace(command.Requirements) ? null : command.Requirements.Trim(),
            StudentNumber = command.StudentNumber.Value,
            Status = command.SubmitImmediately ? TopicStatus.Submitted : TopicStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        topicRepository.Add(topic);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (assignmentInputs.Count > 0 && registrationContract is not null)
        {
            var assignResult = await registrationContract.AssignStudentsByAdvisorAsync(
                topic.Id,
                effectiveAdvisorId,
                isAdmin,
                assignmentInputs,
                cancellationToken);

            if (!assignResult.IsSuccess)
            {
                topicRepository.Remove(topic);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Failure<TopicSummaryDto>(Error.Conflict(
                    assignResult.ErrorCode ?? "TopicRegistration.Failed",
                    assignResult.ErrorMessage ?? "Không thể gán nhóm sinh viên vào đề tài."));
            }
        }

        return Result.Success(new TopicSummaryDto(
            topic.Id,
            topic.BatchId,
            topic.AdvisorId,
            topic.TitleVi,
            topic.TitleEn,
            topic.Description,
            topic.Requirements,
            topic.Status.ToString(),
            topic.StudentNumber,
            topic.CreatedAt,
            topic.IsPublished));
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}

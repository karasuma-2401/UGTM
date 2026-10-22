using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.ReviewSyllabus;

public sealed class ReviewSyllabusHandler(
    IStudentGroupRepository groupRepository,
    ISyllabusRepository syllabusRepository,
    IReviewGateConfigurationRepository gateConfigurationRepository,
    ISubmissionReviewRepository submissionReviewRepository,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<SubmissionReviewDto>> HandleAsync(
        ReviewSyllabusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.ReviewAccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || !Enum.TryParse<ReviewDecision>(command.Decision, ignoreCase: true, out var decision)
            || !Enum.IsDefined(decision))
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.GroupNotFound);
        }

        var batchId = group.Members.Select(member => (Guid?)member.BatchId).FirstOrDefault();
        if (!batchId.HasValue)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            batchId.Value,
            ReviewGateType.Syllabus,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.GateConfigurationNotFound);
        }

        var syllabus = await syllabusRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        if (syllabus is null)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.SubmissionNotFound);
        }

        if (syllabus.Status != SyllabusStatus.Submitted
            || group.Status != StudentGroupStatus.SyllabusSubmitted)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.FinalClosureDeadlinePassed);
        }

        if (syllabus.CurrentVersionNo == 1
            && occurredAt > gateConfiguration.FacultyResponseDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.FacultyResponseDeadlinePassed);
        }

        SubmissionReview review;
        try
        {
            syllabus.RecordReview(
                decision,
                command.Feedback,
                currentUser.UserId!.Value,
                occurredAt);
            review = SubmissionReview.Create(
                Guid.NewGuid(),
                ReviewGateType.Syllabus,
                syllabus.Id,
                syllabus.CurrentVersionNo,
                decision,
                command.Feedback,
                currentUser.UserId.Value,
                occurredAt);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        var groupTransitioned = decision switch
        {
            ReviewDecision.Approved => group.TryApproveSyllabus(),
            ReviewDecision.Rejected => group.TryStop(),
            ReviewDecision.NeedRevision => group.TryOpenSyllabusRevision(),
            _ => false
        };
        if (!groupTransitioned)
        {
            return Result.Failure<SubmissionReviewDto>(SyllabusSubmissionErrors.InvalidReviewState);
        }

        submissionReviewRepository.Add(review);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "Syllabus",
                syllabus.Id.ToString(),
                IntegrationAuditActions.SyllabusReviewed,
                "Succeeded",
                null,
                new
                {
                    syllabus.GroupId,
                    syllabus.CurrentVersionNo,
                    Decision = decision.ToString(),
                    ResultingSyllabusStatus = syllabus.Status.ToString(),
                    ResultingGroupStatus = group.Status.ToString(),
                    FeedbackProvided = !string.IsNullOrWhiteSpace(command.Feedback)
                }),
            occurredAt);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            SyllabusSubmissionErrors.ConcurrencyConflict,
            SyllabusSubmissionErrors.ConcurrencyConflict,
            cancellationToken);
        return saveError is null
            ? Result.Success(SyllabusSubmissionMapper.ToDto(review))
            : Result.Failure<SubmissionReviewDto>(saveError);
    }
}

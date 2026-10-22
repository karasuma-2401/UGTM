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

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.ReviewMidtermReport;

public sealed class ReviewMidtermReportHandler(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
    IReviewGateConfigurationRepository gateConfigurationRepository,
    ISubmissionReviewRepository submissionReviewRepository,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<SubmissionReviewDto>> HandleAsync(
        ReviewMidtermReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanReviewGates(currentUser))
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.ReviewAccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || !Enum.TryParse<ReviewDecision>(command.Decision, ignoreCase: true, out var decision)
            || !Enum.IsDefined(decision))
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.GroupNotFound);
        }

        var batchId = group.Members.Select(member => (Guid?)member.BatchId).FirstOrDefault();
        if (!batchId.HasValue)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            batchId.Value,
            ReviewGateType.Midterm,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.GateConfigurationNotFound);
        }

        var report = await midtermReportRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.SubmissionNotFound);
        }

        if (report.Status != MidtermReportStatus.Submitted
            || group.Status != StudentGroupStatus.MidtermSubmitted)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.FinalClosureDeadlinePassed);
        }

        if (report.CurrentVersionNo == 1
            && occurredAt > gateConfiguration.FacultyResponseDeadlineUtc)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.FacultyResponseDeadlinePassed);
        }

        SubmissionReview review;
        try
        {
            report.RecordReview(
                decision,
                command.Feedback,
                currentUser.UserId!.Value,
                occurredAt);
            review = SubmissionReview.Create(
                Guid.NewGuid(),
                ReviewGateType.Midterm,
                report.Id,
                report.CurrentVersionNo,
                decision,
                command.Feedback,
                currentUser.UserId.Value,
                occurredAt);
        }
        catch (ArgumentException)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        var groupTransitioned = decision switch
        {
            ReviewDecision.Approved => group.TryApproveMidtermReport(),
            ReviewDecision.Rejected => group.TryStop(),
            ReviewDecision.NeedRevision => true,
            _ => false
        };
        if (!groupTransitioned)
        {
            return Result.Failure<SubmissionReviewDto>(MidtermReportSubmissionErrors.InvalidReviewState);
        }

        submissionReviewRepository.Add(review);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "MidtermReport",
                report.Id.ToString(),
                IntegrationAuditActions.MidtermReportReviewed,
                "Succeeded",
                null,
                new
                {
                    report.GroupId,
                    report.CurrentVersionNo,
                    Decision = decision.ToString(),
                    ResultingMidtermReportStatus = report.Status.ToString(),
                    ResultingGroupStatus = group.Status.ToString(),
                    FeedbackProvided = !string.IsNullOrWhiteSpace(command.Feedback)
                }),
            occurredAt);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            MidtermReportSubmissionErrors.ConcurrencyConflict,
            MidtermReportSubmissionErrors.ConcurrencyConflict,
            cancellationToken);
        return saveError is null
            ? Result.Success(SyllabusSubmissionMapper.ToDto(review))
            : Result.Failure<SubmissionReviewDto>(saveError);
    }
}

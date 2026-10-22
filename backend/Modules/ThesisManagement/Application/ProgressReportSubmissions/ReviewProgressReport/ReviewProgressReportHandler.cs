using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.ReviewProgressReport;

public sealed class ReviewProgressReportHandler(
    IProgressMilestoneRepository milestoneRepository,
    IProgressReportRepository progressReportRepository,
    IStudentGroupRepository groupRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ProgressReportReviewDto>> HandleAsync(
        ReviewProgressReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReviewAccessDenied);
        }

        if (command.MilestoneId == Guid.Empty
            || !TryParseDecision(command.Decision, out var decision)
            || (decision == ProgressReportStatus.NeedRevision && string.IsNullOrWhiteSpace(command.Feedback)))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.InvalidRequest);
        }

        var milestone = await milestoneRepository.GetByIdAsync(command.MilestoneId, cancellationToken);
        if (milestone is null)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.MilestoneNotFound);
        }

        var group = await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        if (group is null || !await IsAssignedAdvisorOrAdministratorAsync(group.TopicId, cancellationToken))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReviewAccessDenied);
        }

        var report = await progressReportRepository.GetByMilestoneIdAsync(milestone.Id, cancellationToken);
        if (report is null)
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.ReportNotFound);
        }

        var version = await progressReportRepository.GetVersionAsync(report.Id, report.CurrentVersionNo, cancellationToken);
        if (version is null
            || !report.TryReview(decision, command.Feedback, timeProvider.GetUtcNow().UtcDateTime)
            || !version.TryReview(decision, command.Feedback, currentUser.UserId.Value, report.ReviewedAt!.Value))
        {
            return Result.Failure<ProgressReportReviewDto>(ProgressReportErrors.InvalidReviewState);
        }

        auditOutbox.Add(
            new AuditIntegrationEntry(
                "ProgressReport",
                report.Id.ToString(),
                IntegrationAuditActions.ProgressReportReviewed,
                "Succeeded",
                null,
                new
                {
                    report.MilestoneId,
                    report.GroupId,
                    version.VersionNo,
                    Decision = decision.ToString(),
                    version.IsLate
                }),
            version.ReviewedAt!.Value);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            ProgressReportErrors.ConcurrencyConflict,
            ProgressReportErrors.ConcurrencyConflict,
            cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ProgressReportReviewDto>(saveError);
        }

        return Result.Success(new ProgressReportReviewDto(
            version.VersionNo,
            decision.ToString(),
            version.AdvisorFeedback,
            version.ReviewedBy!.Value,
            version.ReviewedAt!.Value));
    }

    private async Task<bool> IsAssignedAdvisorOrAdministratorAsync(Guid topicId, CancellationToken cancellationToken)
    {
        if (ThesisAuthorization.IsAdministrator(currentUser))
        {
            return true;
        }

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(topicId, cancellationToken);
        return assignment?.AdvisorId == currentUser.UserId!.Value;
    }

    private static bool TryParseDecision(string? value, out ProgressReportStatus decision)
        => Enum.TryParse(value, ignoreCase: true, out decision)
           && decision is ProgressReportStatus.Accepted or ProgressReportStatus.NeedRevision;
}

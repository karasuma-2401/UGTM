using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.BeginFinalReportEvaluation;

public sealed class BeginFinalReportEvaluationHandler(
    IFinalReportRepository finalReportRepository,
    IStudentGroupRepository groupRepository,
    ISubmissionFileRepository submissionFileRepository,
    ITopicAdvisorAssignmentContract advisorAssignmentContract,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<FinalReportEvaluationStartedDto>> HandleAsync(
        BeginFinalReportEvaluationCommand command, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.AuthenticationRequired);
        }
        if (command.FinalReportId == Guid.Empty || command.ExpectedVersionNo < 1)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var report = await finalReportRepository.GetByIdAsync(command.FinalReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.ReportNotFound);
        }
        var group = await groupRepository.GetByIdAsync(report.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.GroupNotFound);
        }
        if (!ThesisAuthorization.IsAdministrator(currentUser))
        {
            var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
            if (assignment?.AdvisorId != currentUser.UserId.Value)
            {
                return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.EvaluationAccessDenied);
            }
        }

        if (group.Status != StudentGroupStatus.FinalReportSubmitted)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.InvalidEvaluationGroupState);
        }
        if (report.VersionType != FinalReportVersionType.InitialFinal || report.Status != FinalReportStatus.Submitted)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.InvalidEvaluationState);
        }
        if (report.CurrentVersionNo != command.ExpectedVersionNo)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.ConcurrencyConflict);
        }

        var files = await submissionFileRepository.GetByEntityAsync(SubmissionEntityType.FinalReport, report.Id, cancellationToken);
        var file = files.SingleOrDefault(value => value.VersionNo == command.ExpectedVersionNo);
        if (file is null)
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.FileNotFound);
        }
        if (!report.TryBeginEvaluation())
        {
            return Result.Failure<FinalReportEvaluationStartedDto>(FinalReportSubmissionErrors.InvalidEvaluationState);
        }
        // The group state must still be valid when this save commits, even though it is unchanged here.
        finalReportRepository.TrackGroupConcurrency(report, group);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        auditOutbox.Add(new AuditIntegrationEntry(
            "FinalReport", report.Id.ToString(), IntegrationAuditActions.FinalReportEvaluationStarted,
            "Succeeded", null, new
            {
                report.GroupId,
                VersionType = report.VersionType.ToString(),
                report.CurrentVersionNo,
                FileId = file.Id,
                file.ChecksumSha256
            }), nowUtc);

        var response = new FinalReportEvaluationStartedDto(report.Id, report.GroupId, report.VersionType.ToString(),
            report.CurrentVersionNo, report.Status.ToString(), currentUser.UserId.Value, nowUtc,
            SyllabusSubmissionMapper.ToDto(file));
        var saveError = await unitOfWork.TrySaveChangesAsync(FinalReportSubmissionErrors.ConcurrencyConflict,
            FinalReportSubmissionErrors.ConcurrencyConflict, cancellationToken);
        return saveError is null ? Result.Success(response) : Result.Failure<FinalReportEvaluationStartedDto>(saveError);
    }
}

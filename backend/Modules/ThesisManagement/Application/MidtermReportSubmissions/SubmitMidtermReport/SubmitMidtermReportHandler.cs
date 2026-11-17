using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.SubmitMidtermReport;

public sealed class SubmitMidtermReportHandler(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
    IReviewGateConfigurationRepository gateConfigurationRepository,
    ISubmissionFileRepository submissionFileRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisUploadLifecycle uploadLifecycle)
{
    public async Task<Result<MidtermReportSubmissionDto>> HandleAsync(
        SubmitMidtermReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.AccessDenied);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            membership.BatchId,
            ReviewGateType.Midterm,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.GateConfigurationNotFound);
        }

        var report = await midtermReportRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        if (report is null)
        {
            if (group.Status != StudentGroupStatus.InProgress)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
            }

            if (occurredAt > gateConfiguration.InitialSubmissionDeadlineUtc)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InitialSubmissionDeadlinePassed);
            }
        }
        else
        {
            if (report.Status != MidtermReportStatus.NeedRevision
                || group.Status != StudentGroupStatus.InProgress)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
            }

            if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.FinalClosureDeadlinePassed);
            }
        }

        ValidatedThesisSubmissionFile validatedFile;
        try
        {
            validatedFile = await fileValidator.ValidateAsync(
                command.FileName,
                command.Content,
                cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidFile);
        }

        var isFirstSubmission = report is null;
        var versionNo = isFirstSubmission ? 1 : checked(report!.CurrentVersionNo + 1);
        var reportId = report?.Id ?? Guid.NewGuid();
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.MidtermReport,
            reportId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.MidtermReport,
            EntityId = reportId,
            VersionNo = versionNo,
            FileName = validatedFile.FileName,
            ObjectKey = objectKey,
            ContentType = validatedFile.ContentType,
            ChecksumSha256 = validatedFile.ChecksumSha256,
            FileSize = validatedFile.FileSize,
            FileExtension = validatedFile.FileExtension,
            UploadedBy = currentUser.UserId.Value,
            UploadedAt = occurredAt
        };

        var uploadAttempt = await uploadLifecycle.BeginAsync(objectKey, cancellationToken);
        try
        {
            await using var content = new MemoryStream(validatedFile.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(new ThesisSubmissionObjectWriteRequest(
                objectKey, validatedFile.ContentType, validatedFile.FileSize), content, cancellationToken);
            if (isFirstSubmission)
            {
                report = MidtermReport.CreateSubmitted(reportId, command.GroupId);
                midtermReportRepository.Add(report);
            }
            else
            {
                report!.SubmitRevision();
            }

            if (!group.TrySubmitMidtermReport())
            {
                return Result.Failure<MidtermReportSubmissionDto>(MidtermReportSubmissionErrors.InvalidSubmissionState);
            }

            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "MidtermReport",
                    report.Id.ToString(),
                    IntegrationAuditActions.MidtermReportSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        report.GroupId,
                        report.CurrentVersionNo,
                        FileId = submissionFile.Id,
                        submissionFile.FileName,
                        submissionFile.FileSize,
                        submissionFile.ContentType,
                        IsResubmission = !isFirstSubmission
                    }),
                occurredAt);

            var saveError = await uploadLifecycle.SaveAsync(uploadAttempt,
                token => unitOfWork.TrySaveChangesAsync(MidtermReportSubmissionErrors.ConcurrencyConflict,
                    MidtermReportSubmissionErrors.ConcurrencyConflict, token),
                MidtermReportSubmissionErrors.ConcurrencyConflict, cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<MidtermReportSubmissionDto>(saveError);
            }

            return Result.Success(MidtermReportSubmissionMapper.ToSubmissionDto(
                report,
                SyllabusSubmissionMapper.ToDto(submissionFile)));
        }
        finally
        {
            await uploadLifecycle.FinishAsync(uploadAttempt);
        }
    }
}

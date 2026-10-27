using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.SubmitFinalReport;

public sealed class SubmitFinalReportHandler(
    FinalReportSubmissionEligibilityChecker eligibilityChecker,
    IFinalReportRepository finalReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ILogger<SubmitFinalReportHandler> logger)
{
    public async Task<Result<FinalReportSubmissionDto>> HandleAsync(
        SubmitFinalReportCommand command,
        CancellationToken cancellationToken = default)
    {
        var eligibility = await eligibilityChecker.CheckAsync(command.GroupId, cancellationToken);
        if (eligibility.IsFailure)
        {
            return Result.Failure<FinalReportSubmissionDto>(eligibility.Error);
        }

        var context = eligibility.Value;
        var existingReport = await finalReportRepository.GetByGroupAndVersionTypeAsync(
            context.Group.Id, FinalReportVersionType.InitialFinal, cancellationToken);
        if (existingReport is not null)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.AlreadySubmitted);
        }

        if (context.Group.Status != StudentGroupStatus.InProgress)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidGroupState);
        }

        if (command.Content is null || !command.Content.CanRead)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidFile);
        }

        ValidatedThesisSubmissionFile file;
        try
        {
            file = await fileValidator.ValidateAsync(command.FileName, command.Content, cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidFile);
        }

        var report = FinalReport.CreateSubmitted(
            Guid.NewGuid(), context.Group.Id, FinalReportVersionType.InitialFinal, context.SubmittedAtUtc);
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.FinalReport, report.Id, report.CurrentVersionNo, file.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.FinalReport,
            EntityId = report.Id,
            VersionNo = report.CurrentVersionNo,
            FileName = file.FileName,
            ObjectKey = objectKey,
            ContentType = file.ContentType,
            ChecksumSha256 = file.ChecksumSha256,
            FileSize = file.FileSize,
            FileExtension = file.FileExtension,
            UploadedBy = context.StudentId,
            UploadedAt = context.SubmittedAtUtc
        };
        var response = new FinalReportSubmissionDto(
            report.Id, report.GroupId, report.VersionType.ToString(), report.CurrentVersionNo,
            report.Status.ToString(), report.SubmittedAt, SyllabusSubmissionMapper.ToDto(submissionFile));

        var saveAttempted = false;
        try
        {
            await using var content = new MemoryStream(file.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(
                new ThesisSubmissionObjectWriteRequest(objectKey, file.ContentType, file.FileSize),
                content, cancellationToken);

            if (!context.Group.TrySubmitFinalReport())
            {
                await TryDeleteObjectAsync(objectKey);
                return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidGroupState);
            }

            finalReportRepository.Add(report);
            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(new AuditIntegrationEntry(
                "FinalReport",
                report.Id.ToString(),
                IntegrationAuditActions.FinalReportSubmitted,
                "Succeeded",
                null,
                new
                {
                    report.GroupId,
                    context.BatchId,
                    VersionType = report.VersionType.ToString(),
                    report.CurrentVersionNo,
                    FileId = submissionFile.Id,
                    submissionFile.FileName,
                    submissionFile.FileSize,
                    submissionFile.ContentType,
                    submissionFile.ChecksumSha256,
                    IsResubmission = false
                }), context.SubmittedAtUtc);

            saveAttempted = true;
            var saveError = await unitOfWork.TrySaveChangesAsync(
                FinalReportSubmissionErrors.ConcurrencyConflict,
                FinalReportSubmissionErrors.ConcurrencyConflict,
                cancellationToken);
            if (saveError is not null)
            {
                await TryDeleteObjectAsync(objectKey);
                return Result.Failure<FinalReportSubmissionDto>(saveError);
            }
        }
        catch
        {
            await CompensateFailedSubmissionAsync(submissionFile, saveAttempted);
            throw;
        }

        return Result.Success(response);
    }

    private async Task CompensateFailedSubmissionAsync(SubmissionFile file, bool saveAttempted)
    {
        if (saveAttempted)
        {
            // A connection/cancellation failure can arrive after commit. Never delete an object
            // that the database already references, even when the original request reports failure.
            using var verification = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                if (await submissionFileRepository.GetByIdAsync(file.Id, verification.Token) is not null)
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Could not verify final report save for object {ObjectKey}; retaining the object until its database reference can be checked.",
                    file.ObjectKey);
                return;
            }
        }

        await TryDeleteObjectAsync(file.ObjectKey);
    }

    private async Task TryDeleteObjectAsync(string objectKey)
    {
        // Request cancellation must not cancel compensation for an uploaded object.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await objectStorage.DeleteAsync(objectKey, cleanup.Token);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception,
                "Could not delete unreferenced final report object {ObjectKey}; cleanup must be retried.", objectKey);
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
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
    IFinalReportPlagiarismScanRepository plagiarismScanRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ILogger<SubmitFinalReportHandler> logger,
    IOptions<PlagiarismAiOptions>? plagiarismOptions = null,
    ISubmissionProductLinkRepository? productLinkRepository = null,
    ISubmissionAttachmentRepository? attachmentRepository = null,
    IThesisSubmissionImageValidator? imageValidator = null)
{
    public async Task<Result<FinalReportSubmissionDto>> HandleAsync(
        SubmitFinalReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!SubmissionProductLinkValidator.TryNormalize(
                command.ProductLinks,
                null,
                requireAtLeastOne: false,
                out var normalizedLinks))
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var eligibility = await eligibilityChecker.CheckAsync(command.GroupId, cancellationToken);
        if (eligibility.IsFailure)
        {
            return Result.Failure<FinalReportSubmissionDto>(eligibility.Error);
        }

        var context = eligibility.Value;
        var existingReport = await finalReportRepository.GetByGroupAndVersionTypeAsync(
            context.Group.Id, FinalReportVersionType.InitialFinal, cancellationToken);
        var isFirstSubmission = existingReport is null;
        if (existingReport is not null && existingReport.Status != FinalReportStatus.Submitted)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.EvaluationStarted);
        }

        var requiredGroupStatus = isFirstSubmission
            ? StudentGroupStatus.InProgress
            : StudentGroupStatus.FinalReportSubmitted;
        if (context.Group.Status != requiredGroupStatus)
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

        var report = existingReport ?? FinalReport.CreateSubmitted(
            Guid.NewGuid(), context.Group.Id, FinalReportVersionType.InitialFinal, context.SubmittedAtUtc);
        var versionNo = isFirstSubmission ? 1 : checked(report.CurrentVersionNo + 1);

        IReadOnlyList<SubmissionEvidenceHelper.PreparedAttachment> preparedAttachments;
        try
        {
            preparedAttachments = await SubmissionEvidenceHelper.PrepareImagesAsync(
                command.EvidenceImages,
                imageValidator,
                objectKeyFactory,
                SubmissionEntityType.FinalReport,
                report.Id,
                versionNo,
                context.StudentId,
                context.SubmittedAtUtc,
                cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidAttachment);
        }

        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.FinalReport, report.Id, versionNo, file.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.FinalReport,
            EntityId = report.Id,
            VersionNo = versionNo,
            FileName = file.FileName,
            ObjectKey = objectKey,
            ContentType = file.ContentType,
            ChecksumSha256 = file.ChecksumSha256,
            FileSize = file.FileSize,
            FileExtension = file.FileExtension,
            UploadedBy = context.StudentId,
            UploadedAt = context.SubmittedAtUtc
        };
        var linkEntities = SubmissionEvidenceHelper.BuildProductLinks(
            normalizedLinks,
            SubmissionEntityType.FinalReport,
            report.Id,
            versionNo,
            context.StudentId,
            context.SubmittedAtUtc);
        var attachmentEntities = preparedAttachments.Select(item => item.Entity).ToArray();
        var uploadedKeys = new List<string>(1 + preparedAttachments.Count);

        var response = new FinalReportSubmissionDto(
            report.Id,
            report.GroupId,
            report.VersionType.ToString(),
            versionNo,
            report.Status.ToString(),
            context.SubmittedAtUtc,
            SyllabusSubmissionMapper.ToDto(submissionFile),
            linkEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray(),
            attachmentEntities.Select(SubmissionSupplementalMapper.ToDto).ToArray());

        var saveAttempted = false;
        try
        {
            await using var content = new MemoryStream(file.Content.ToArray(), writable: false);
            await objectStorage.UploadAsync(
                new ThesisSubmissionObjectWriteRequest(objectKey, file.ContentType, file.FileSize),
                content, cancellationToken);
            uploadedKeys.Add(objectKey);
            await SubmissionEvidenceHelper.UploadAllAsync(preparedAttachments, objectStorage, uploadedKeys, cancellationToken);

            if (context.Group.Status != requiredGroupStatus
                || (isFirstSubmission && !context.Group.TrySubmitFinalReport()))
            {
                await TryDeleteObjectsAsync(uploadedKeys);
                return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.InvalidGroupState);
            }

            if (isFirstSubmission)
            {
                finalReportRepository.Add(report);
            }
            else
            {
                if (!report.TrySubmitRevision(context.SubmittedAtUtc))
                {
                    await TryDeleteObjectsAsync(uploadedKeys);
                    return Result.Failure<FinalReportSubmissionDto>(FinalReportSubmissionErrors.ConcurrencyConflict);
                }

                finalReportRepository.TrackGroupConcurrency(report, context.Group);
            }
            submissionFileRepository.Add(submissionFile);
            if (linkEntities.Count > 0)
            {
                productLinkRepository?.AddRange(linkEntities);
            }
            if (attachmentEntities.Length > 0)
            {
                attachmentRepository?.AddRange(attachmentEntities);
            }

            var plagiarismScan = FinalReportPlagiarismScan.Create(
                Guid.NewGuid(),
                report.Id,
                submissionFile.Id,
                versionNo,
                context.SubmittedAtUtc,
                plagiarismOptions?.Value.GetNormalizedWorkerPartition());
            plagiarismScanRepository.Add(plagiarismScan);
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
                    LinkCount = linkEntities.Count,
                    AttachmentCount = attachmentEntities.Length,
                    IsResubmission = !isFirstSubmission
                }), context.SubmittedAtUtc);

            saveAttempted = true;
            var saveError = await unitOfWork.TrySaveChangesAsync(
                FinalReportSubmissionErrors.ConcurrencyConflict,
                FinalReportSubmissionErrors.ConcurrencyConflict,
                cancellationToken);
            if (saveError is not null)
            {
                await TryDeleteObjectsAsync(uploadedKeys);
                return Result.Failure<FinalReportSubmissionDto>(saveError);
            }
        }
        catch
        {
            await CompensateFailedSubmissionAsync(submissionFile, uploadedKeys, saveAttempted);
            throw;
        }

        return Result.Success(response);
    }

    private async Task CompensateFailedSubmissionAsync(SubmissionFile file, IReadOnlyCollection<string> uploadedKeys, bool saveAttempted)
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

        await TryDeleteObjectsAsync(uploadedKeys.Count > 0 ? uploadedKeys : [file.ObjectKey]);
    }

    private async Task TryDeleteObjectsAsync(IEnumerable<string> objectKeys)
    {
        foreach (var objectKey in objectKeys)
        {
            await TryDeleteObjectAsync(objectKey);
        }
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

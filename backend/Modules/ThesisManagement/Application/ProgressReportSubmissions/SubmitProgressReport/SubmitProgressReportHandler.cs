using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.SubmitProgressReport;

public sealed class SubmitProgressReportHandler(
    IProgressMilestoneRepository milestoneRepository,
    IProgressReportRepository progressReportRepository,
    IStudentGroupRepository groupRepository,
    ISubmissionFileRepository submissionFileRepository,
    IThesisSubmissionFileValidator fileValidator,
    IThesisSubmissionObjectKeyFactory objectKeyFactory,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ProgressReportSubmissionDto>> HandleAsync(
        SubmitProgressReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.AccessDenied);
        }

        if (command.MilestoneId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead
            || !TryNormalizeGitUrl(command.GitUrl, out var gitUrl))
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidRequest);
        }

        var milestone = await milestoneRepository.GetByIdAsync(command.MilestoneId, cancellationToken);
        if (milestone is null)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.MilestoneNotFound);
        }

        var group = await groupRepository.GetByIdAsync(milestone.GroupId, cancellationToken);
        if (group is null || !group.Members.Any(member => member.StudentId == currentUser.UserId.Value))
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.AccessDenied);
        }

        if (group.Status != StudentGroupStatus.InProgress)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var report = await progressReportRepository.GetByMilestoneIdAsync(milestone.Id, cancellationToken);
        var isFirstSubmission = report is null;
        var versionNo = isFirstSubmission ? 1 : checked(report!.CurrentVersionNo + 1);
        if (!isFirstSubmission && report!.Status != ProgressReportStatus.NeedRevision)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidSubmissionState);
        }

        ValidatedThesisSubmissionFile validatedFile;
        try
        {
            validatedFile = await fileValidator.ValidateAsync(command.FileName, command.Content, cancellationToken);
        }
        catch (ThesisSubmissionFileValidationException)
        {
            return Result.Failure<ProgressReportSubmissionDto>(ProgressReportErrors.InvalidFile);
        }

        var reportId = report?.Id ?? Guid.NewGuid();
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.ProgressReport,
            reportId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.ProgressReport,
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

        await using var content = new MemoryStream(validatedFile.Content.ToArray(), writable: false);
        await objectStorage.UploadAsync(
            new ThesisSubmissionObjectWriteRequest(objectKey, validatedFile.ContentType, validatedFile.FileSize),
            content,
            cancellationToken);

        try
        {
            if (isFirstSubmission)
            {
                report = ProgressReport.CreateSubmitted(reportId, milestone.Id, group.Id, command.StudentNote, occurredAt);
                progressReportRepository.Add(report);
            }
            else if (!report!.TrySubmitRevision(command.StudentNote, occurredAt))
            {
                return await DeleteObjectAndFailAsync(objectKey, ProgressReportErrors.InvalidSubmissionState, cancellationToken);
            }

            var version = ProgressReportVersion.CreateSubmitted(
                Guid.NewGuid(),
                report!.Id,
                versionNo,
                gitUrl!,
                command.StudentNote,
                currentUser.UserId.Value,
                occurredAt,
                occurredAt > milestone.Deadline);
            progressReportRepository.AddVersion(version);
            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "ProgressReport",
                    report.Id.ToString(),
                    IntegrationAuditActions.ProgressReportSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        report.MilestoneId,
                        report.GroupId,
                        version.VersionNo,
                        version.IsLate,
                        version.GitUrl,
                        FileId = submissionFile.Id,
                        submissionFile.FileName
                    }),
                occurredAt);

            var saveError = await unitOfWork.TrySaveChangesAsync(
                ProgressReportErrors.ConcurrencyConflict,
                ProgressReportErrors.ConcurrencyConflict,
                cancellationToken);
            if (saveError is not null)
            {
                return await DeleteObjectAndFailAsync(objectKey, saveError, cancellationToken);
            }

            return Result.Success(new ProgressReportSubmissionDto(
                report.Id,
                report.MilestoneId,
                report.GroupId,
                version.VersionNo,
                report.Status.ToString(),
                version.GitUrl,
                version.StudentNote,
                version.IsLate,
                version.SubmittedAt,
                SyllabusSubmissionMapper.ToDto(submissionFile)));
        }
        catch
        {
            await TryDeleteObjectAsync(objectKey, cancellationToken);
            throw;
        }
    }

    private static bool TryNormalizeGitUrl(string? value, out string? normalizedUrl)
    {
        normalizedUrl = null;
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 2048)
        {
            return false;
        }

        var candidate = value.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }

    private async Task<Result<ProgressReportSubmissionDto>> DeleteObjectAndFailAsync(string objectKey, Error error, CancellationToken cancellationToken)
    {
        await TryDeleteObjectAsync(objectKey, cancellationToken);
        return Result.Failure<ProgressReportSubmissionDto>(error);
    }

    private async Task TryDeleteObjectAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await objectStorage.DeleteAsync(objectKey, cancellationToken);
        }
        catch
        {
            // The object is unreachable without a committed database row; an orphan sweep can retry cleanup.
        }
    }
}

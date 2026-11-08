using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.SubmitSyllabus;

public sealed class SubmitSyllabusHandler(
    IStudentGroupRepository groupRepository,
    ISyllabusRepository syllabusRepository,
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
    public async Task<Result<SyllabusSubmissionDto>> HandleAsync(
        SubmitSyllabusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        if (command.GroupId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.FileName)
            || command.Content is null
            || !command.Content.CanRead)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(command.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.AccessDenied);
        }

        var gateConfiguration = await gateConfigurationRepository.GetByBatchAndGateAsync(
            membership.BatchId,
            ReviewGateType.Syllabus,
            cancellationToken);
        if (gateConfiguration is null)
        {
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.GateConfigurationNotFound);
        }

        var syllabus = await syllabusRepository.GetByGroupIdAsync(command.GroupId, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var isRenameResubmission = group.AllowSyllabusResubmission;

        if (isRenameResubmission)
        {
            if (syllabus is null)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
            }
        }
        else if (syllabus is null)
        {
            if (group.Status != StudentGroupStatus.Registered)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
            }

            if (occurredAt > gateConfiguration.InitialSubmissionDeadlineUtc)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InitialSubmissionDeadlinePassed);
            }
        }
        else
        {
            if (syllabus.Status != SyllabusStatus.NeedRevision
                || group.Status != StudentGroupStatus.SyllabusRevisionRequired)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
            }

            if (occurredAt > gateConfiguration.FinalClosureDeadlineUtc)
            {
                return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.FinalClosureDeadlinePassed);
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
            return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidFile);
        }

        var isFirstSubmission = syllabus is null;
        var versionNo = isFirstSubmission ? 1 : checked(syllabus!.CurrentVersionNo + 1);
        var syllabusId = syllabus?.Id ?? Guid.NewGuid();
        var objectKey = objectKeyFactory.Create(
            SubmissionEntityType.Syllabus,
            syllabusId,
            versionNo,
            validatedFile.FileExtension);
        var submissionFile = new SubmissionFile
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.Syllabus,
            EntityId = syllabusId,
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
            if (isRenameResubmission)
            {
                syllabus!.SubmitUpdatedAfterRename();
                group.ConsumeSyllabusResubmission();
            }
            else if (isFirstSubmission)
            {
                syllabus = Syllabus.CreateSubmitted(syllabusId, command.GroupId);
                syllabusRepository.Add(syllabus);

                if (!group.TrySubmitSyllabus())
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
                }
            }
            else
            {
                syllabus!.SubmitRevision();

                if (!group.TrySubmitSyllabus())
                {
                    return Result.Failure<SyllabusSubmissionDto>(SyllabusSubmissionErrors.InvalidSubmissionState);
                }
            }

            submissionFileRepository.Add(submissionFile);
            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "Syllabus",
                    syllabus.Id.ToString(),
                    IntegrationAuditActions.SyllabusSubmitted,
                    "Succeeded",
                    null,
                    new
                    {
                        syllabus.GroupId,
                        syllabus.CurrentVersionNo,
                        FileId = submissionFile.Id,
                        submissionFile.FileName,
                        submissionFile.FileSize,
                        submissionFile.ContentType,
                        IsResubmission = !isFirstSubmission
                    }),
                occurredAt);

            var saveError = await uploadLifecycle.SaveAsync(uploadAttempt,
                token => unitOfWork.TrySaveChangesAsync(SyllabusSubmissionErrors.ConcurrencyConflict,
                    SyllabusSubmissionErrors.ConcurrencyConflict, token),
                SyllabusSubmissionErrors.ConcurrencyConflict, cancellationToken);
            if (saveError is not null)
            {
                return Result.Failure<SyllabusSubmissionDto>(saveError);
            }

            return Result.Success(SyllabusSubmissionMapper.ToSubmissionDto(syllabus, submissionFile));
        }
        finally
        {
            await uploadLifecycle.FinishAsync(uploadAttempt);
        }
    }
}

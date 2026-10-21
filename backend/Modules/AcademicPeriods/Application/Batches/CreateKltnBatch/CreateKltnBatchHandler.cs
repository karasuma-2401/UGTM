using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.CreateKltnBatch;

public sealed class CreateKltnBatchHandler(
    IKltnBatchRepository batchRepository,
    IScheduleTemplateRepository templateRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public const string DefaultTemplateCode = "UIT_KLTN_STANDARD";

    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        CreateKltnBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        if (string.IsNullOrWhiteSpace(command.BatchName)
            || string.IsNullOrWhiteSpace(command.AcademicYear)
            || command.Semester <= 0
            || string.IsNullOrWhiteSpace(command.TemplateCode))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }

        if (await batchRepository.ExistsAsync(command.AcademicYear, command.Semester, cancellationToken))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.DuplicateAcademicPeriod);
        }

        var template = await templateRepository.GetActiveAsync(command.TemplateCode, cancellationToken);
        if (template is null)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.ActiveTemplateNotFound);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        KltnBatch batch;
        try
        {
            var materializedSchedule = template.Materialize(command.StartDate);
            batch = KltnBatch.Create(
                Guid.NewGuid(),
                command.BatchName,
                command.AcademicYear,
                command.Semester,
                command.StartDate,
                materializedSchedule[^1].EndDate,
                template,
                currentUser.UserId!.Value,
                occurredAt);
        }
        catch (ArgumentException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }

        batchRepository.Add(batch);
        auditOutbox.Add(
            new AuditIntegrationEntry(
                "KltnBatch",
                batch.Id.ToString(),
                IntegrationAuditActions.BatchCreated,
                "Succeeded",
                null,
                new
                {
                    batch.AcademicYear,
                    batch.Semester,
                    batch.ScheduleTemplateVersionId,
                    StageCount = batch.Stages.Count
                }),
            occurredAt);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            AcademicBatchErrors.ConcurrencyConflict,
            AcademicBatchErrors.DuplicateAcademicPeriod,
            cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<KltnBatchDetailDto>(saveError);
        }

        return Result.Success(AcademicBatchMapper.ToDetail(batch));
    }
}

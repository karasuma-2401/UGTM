using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.AcademicPeriods.Domain.Scheduling;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.UpdateKltnBatchSchedule;

public sealed class UpdateKltnBatchScheduleHandler(
    IKltnBatchRepository batchRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        UpdateKltnBatchScheduleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty || command.Stages is null)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }

        var batch = await batchRepository.GetByIdAsync(command.BatchId, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound);
        }

        var schedule = command.Stages.Select(stage => new StageSchedule(
            stage.OrderIndex,
            stage.StageCode,
            stage.StageName,
            stage.StartDate,
            stage.EndDate,
            stage.DeadlineAtUtc.UtcDateTime)).ToArray();
        try
        {
            batch.ValidateScheduleReplacement(schedule, command.ChangeReason);
        }
        catch (ArgumentException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidRequest);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidStatusTransition);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                // Delete existing rows first. Recreating seven stages in one SaveChanges call makes EF
                // combine the relationship changes with the batch xmin update, which can produce a
                // false optimistic-concurrency conflict.
                batch.RemoveStagesForScheduleReplacement(command.ChangeReason);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                await batchRepository.ReloadAsync(batch, transactionCancellationToken);

                var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
                batch.ReplaceSchedule(
                    schedule,
                    command.ChangeReason,
                    currentUser.UserId!.Value,
                    occurredAt);
                batchRepository.AddStages(batch.Stages);

                auditOutbox.Add(
                    new AuditIntegrationEntry(
                        "KltnBatch",
                        batch.Id.ToString(),
                        IntegrationAuditActions.BatchScheduleUpdated,
                        "Succeeded",
                        null,
                        new
                        {
                            ChangeReason = string.IsNullOrWhiteSpace(command.ChangeReason) ? null : command.ChangeReason.Trim(),
                            Stages = batch.Stages.OrderBy(x => x.OrderIndex).Select(x => new
                            {
                                x.OrderIndex,
                                x.StageCode,
                                x.StartDate,
                                x.EndDate,
                                x.DeadlineAtUtc
                            })
                        }),
                    occurredAt);

                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(AcademicBatchMapper.ToDetail(batch));
            }, cancellationToken);
        }
        catch (AcademicPeriodsConcurrencyException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.ConcurrencyConflict);
        }
        catch (AcademicPeriodsUniqueConstraintException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.ConcurrencyConflict);
        }
    }
}

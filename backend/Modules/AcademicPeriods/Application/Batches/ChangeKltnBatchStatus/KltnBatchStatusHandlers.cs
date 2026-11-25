using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Notifications;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.ChangeKltnBatchStatus;

public sealed class OpenKltnBatchHandler(
    IKltnBatchRepository batchRepository,
    IScoreConfigurationRepository scoreConfigurationRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IAcademicPeriodsNotificationOutbox? notificationOutbox = null)
{
    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        OpenKltnBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                await batchRepository.AcquireOpenBatchLockAsync(transactionCancellationToken);

                var batch = await GetBatchAsync(command.BatchId, batchRepository, transactionCancellationToken);
                if (batch is null)
                {
                    return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound);
                }

                if (await batchRepository.ExistsOpenBatchAsync(batch.Id, transactionCancellationToken))
                {
                    return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AnotherBatchAlreadyOpen);
                }

                var activeConfiguration = await scoreConfigurationRepository.GetActiveByBatchAsync(
                    batch.Id,
                    transactionCancellationToken);
                if (activeConfiguration is null)
                {
                    return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.ScoreConfigurationRequired);
                }

                var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
                try
                {
                    batch.Open(activeConfiguration, currentUser.UserId!.Value, occurredAt);
                }
                catch (InvalidOperationException)
                {
                    return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidStatusTransition);
                }

                AddAudit(auditOutbox, batch, IntegrationAuditActions.BatchOpened, occurredAt);
                notificationOutbox?.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "BATCH_OPENED",
                        Title: $"Đợt KLTN {batch.BatchName} đã mở",
                        Content: $"Đợt {batch.BatchName} (HK{batch.Semester}, {batch.AcademicYear}) đã mở chính thức. Vui lòng theo dõi lịch trình các giai đoạn.",
                        RecipientRoles: ["Student", "Lecturer", "Faculty", "AcademicAffairs"],
                        ExcludeUserIds: currentUser.UserId.HasValue ? [currentUser.UserId.Value] : null,
                        ResourceType: "KltnBatch",
                        ResourceId: batch.Id,
                        AcademicBatchId: batch.Id,
                        ActionPath: "/dashboard",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAt);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(AcademicBatchMapper.ToDetail(batch));
            }, cancellationToken);
        }
        catch (AcademicPeriodsPersistenceException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.ConcurrencyConflict);
        }
    }

    internal static Task<KltnBatch?> GetBatchAsync(
        Guid batchId,
        IKltnBatchRepository repository,
        CancellationToken cancellationToken)
        => batchId == Guid.Empty
            ? Task.FromResult<KltnBatch?>(null)
            : repository.GetByIdAsync(batchId, cancellationToken);

    internal static void AddAudit(
        IAcademicPeriodsAuditOutbox auditOutbox,
        KltnBatch batch,
        string action,
        DateTime occurredAt)
        => auditOutbox.Add(
            new AuditIntegrationEntry(
                "KltnBatch",
                batch.Id.ToString(),
                action,
                "Succeeded",
                null,
                new { Status = batch.Status.ToString() }),
            occurredAt);
}

public sealed class CloseKltnBatchHandler(
    IKltnBatchRepository batchRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        CloseKltnBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        var batch = await OpenKltnBatchHandler.GetBatchAsync(command.BatchId, batchRepository, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            batch.Close(currentUser.UserId!.Value, occurredAt);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidStatusTransition);
        }

        OpenKltnBatchHandler.AddAudit(auditOutbox, batch, IntegrationAuditActions.BatchClosed, occurredAt);
        var saveError = await unitOfWork.TrySaveChangesAsync(
            AcademicBatchErrors.ConcurrencyConflict,
            cancellationToken: cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<KltnBatchDetailDto>(saveError);
        }

        return Result.Success(AcademicBatchMapper.ToDetail(batch));
    }
}

public sealed class ArchiveKltnBatchHandler(
    IKltnBatchRepository batchRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<KltnBatchDetailDto>> HandleAsync(
        ArchiveKltnBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageBatches(currentUser))
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.AccessDenied);
        }

        var batch = await OpenKltnBatchHandler.GetBatchAsync(command.BatchId, batchRepository, cancellationToken);
        if (batch is null)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.NotFound);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            batch.Archive(currentUser.UserId!.Value, occurredAt);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<KltnBatchDetailDto>(AcademicBatchErrors.InvalidStatusTransition);
        }

        OpenKltnBatchHandler.AddAudit(auditOutbox, batch, IntegrationAuditActions.BatchArchived, occurredAt);
        var saveError = await unitOfWork.TrySaveChangesAsync(
            AcademicBatchErrors.ConcurrencyConflict,
            cancellationToken: cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<KltnBatchDetailDto>(saveError);
        }

        return Result.Success(AcademicBatchMapper.ToDetail(batch));
    }
}

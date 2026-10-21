using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Domain.Enums;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;

public sealed class PublishScoreConfigurationHandler(
    IScoreConfigurationRepository configurationRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ScoreConfigurationDto>> HandleAsync(
        PublishScoreConfigurationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageScoreConfigurations(currentUser))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty || command.ConfigurationId == Guid.Empty)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var configuration = await configurationRepository.GetByIdAsync(
                    command.ConfigurationId,
                    transactionCancellationToken);
                if (configuration is null || configuration.BatchId != command.BatchId)
                {
                    return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.NotFound);
                }

                if (configuration.Status != ConfigurationVersionStatus.Draft)
                {
                    return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidStatusTransition);
                }

                var previousActive = await configurationRepository.GetActiveByBatchAsync(
                    command.BatchId,
                    transactionCancellationToken);
                var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
                if (previousActive is not null)
                {
                    previousActive.Retire(occurredAt);
                    auditOutbox.Add(
                        CreateScoreConfigurationDraftHandler.AuditEntry(
                            previousActive,
                            IntegrationAuditActions.ScoreConfigurationRetired,
                            new { ReplacedByConfigurationId = configuration.Id }),
                        occurredAt);

                    // Flush the retirement first so PostgreSQL's immediate filtered unique
                    // index never observes two Active rows. The enclosing transaction keeps
                    // both saves and both audit messages atomic.
                    await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                }

                configuration.Publish(currentUser.UserId!.Value, occurredAt);
                auditOutbox.Add(
                    CreateScoreConfigurationDraftHandler.AuditEntry(
                        configuration,
                        IntegrationAuditActions.ScoreConfigurationPublished,
                        new { PreviousActiveConfigurationId = previousActive?.Id }),
                    occurredAt);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ScoreConfigurationMapper.ToDto(configuration));
            }, cancellationToken);
        }
        catch (AcademicPeriodsPersistenceException)
        {
            // Catch outside the transaction delegate so any partial retirement/audit write
            // is rolled back before the conflict is returned to the caller.
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ConcurrencyConflict);
        }
    }
}

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

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.UpdateDraft;

public sealed class UpdateScoreConfigurationDraftHandler(
    IScoreConfigurationRepository configurationRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ScoreConfigurationDto>> HandleAsync(
        UpdateScoreConfigurationDraftCommand command,
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

        var configuration = await configurationRepository.GetByIdAsync(command.ConfigurationId, cancellationToken);
        if (configuration is null || configuration.BatchId != command.BatchId)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.NotFound);
        }

        if (configuration.Status != ConfigurationVersionStatus.Draft)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ActiveNotEditable);
        }

        if (configuration.VersionNo > 1 && string.IsNullOrWhiteSpace(command.ChangeReason))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ChangeReasonRequired);
        }

        var parsed = ScoreConfigurationInputParser.Parse(
            command.CalculationMethod,
            command.Components,
            allowIncompleteTotal: true);
        if (parsed.IsFailure)
        {
            return Result.Failure<ScoreConfigurationDto>(parsed.Error);
        }

        try
        {
            configuration.ValidateDraftUpdate(
                parsed.Value.CalculationMethod,
                parsed.Value.Components,
                command.ChangeReason,
                allowIncompleteTotal: true);
        }
        catch (ArgumentException)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                // Flush deletion of existing components before inserting replacements so
                // PostgreSQL unique indexes on (config_id, component_code) / (config_id, order_index)
                // and ScoreWeightConfig xmin concurrency tokens never produce false 409 conflicts.
                configuration.RemoveComponentsForReplacement();
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                await configurationRepository.ReloadAsync(configuration, transactionCancellationToken);

                configuration.UpdateDraft(
                    parsed.Value.CalculationMethod,
                    parsed.Value.Components,
                    occurredAt,
                    command.ChangeReason,
                    allowIncompleteTotal: true);

                configurationRepository.AddComponents(configuration.Components);
                auditOutbox.Add(
                    CreateScoreConfigurationDraftHandler.AuditEntry(
                        configuration,
                        IntegrationAuditActions.ScoreConfigurationDraftUpdated),
                    occurredAt);
                await unitOfWork.SaveChangesAsync(transactionCancellationToken);
                return Result.Success(ScoreConfigurationMapper.ToDto(configuration));
            }, cancellationToken);
        }
        catch (AcademicPeriodsPersistenceException)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ConcurrencyConflict);
        }
    }
}

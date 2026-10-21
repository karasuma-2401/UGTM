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

        var parsed = ScoreConfigurationInputParser.Parse(command.CalculationMethod, command.Components);
        if (parsed.IsFailure)
        {
            return Result.Failure<ScoreConfigurationDto>(parsed.Error);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            configuration.UpdateDraft(
                parsed.Value.CalculationMethod,
                parsed.Value.Components,
                occurredAt,
                command.ChangeReason);
        }
        catch (ArgumentException)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        auditOutbox.Add(
            CreateScoreConfigurationDraftHandler.AuditEntry(
                configuration,
                IntegrationAuditActions.ScoreConfigurationDraftUpdated),
            occurredAt);
        var saveError = await unitOfWork.TrySaveChangesAsync(
            ScoreConfigurationErrors.ConcurrencyConflict,
            cancellationToken: cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ScoreConfigurationDto>(saveError);
        }

        return Result.Success(ScoreConfigurationMapper.ToDto(configuration));
    }
}

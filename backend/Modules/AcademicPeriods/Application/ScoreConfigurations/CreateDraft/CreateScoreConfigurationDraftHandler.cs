using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Auditing;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.Persistence;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Domain.Entities;
using UGTM.Modules.AcademicPeriods.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.CreateDraft;

public sealed class CreateScoreConfigurationDraftHandler(
    IKltnBatchRepository batchRepository,
    IScoreConfigurationRepository configurationRepository,
    IAcademicPeriodsAuditOutbox auditOutbox,
    IAcademicPeriodsUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ScoreConfigurationDto>> HandleAsync(
        CreateScoreConfigurationDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanManageScoreConfigurations(currentUser))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (await batchRepository.GetByIdAsync(command.BatchId, cancellationToken) is null)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.BatchNotFound);
        }

        if (await configurationRepository.GetDraftByBatchAsync(command.BatchId, cancellationToken) is not null)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.DraftAlreadyExists);
        }

        var active = await configurationRepository.GetActiveByBatchAsync(command.BatchId, cancellationToken);
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        ScoreWeightConfig draft;

        if (active is not null)
        {
            if (string.IsNullOrWhiteSpace(command.ChangeReason))
            {
                return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ChangeReasonRequired);
            }

            try
            {
                draft = active.CloneAsNextDraft(
                    Guid.NewGuid(),
                    currentUser.UserId!.Value,
                    occurredAt,
                    command.ChangeReason);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
            }

            var hasReplacement = command.CalculationMethod is not null || command.Components is not null;
            if (hasReplacement)
            {
                if (command.CalculationMethod is null || command.Components is null)
                {
                    return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
                }

                var parsedReplacement = ScoreConfigurationInputParser.Parse(
                    command.CalculationMethod,
                    command.Components);
                if (parsedReplacement.IsFailure)
                {
                    return Result.Failure<ScoreConfigurationDto>(parsedReplacement.Error);
                }

                try
                {
                    draft.UpdateDraft(
                        parsedReplacement.Value.CalculationMethod,
                        parsedReplacement.Value.Components,
                        occurredAt,
                        command.ChangeReason);
                }
                catch (ArgumentException)
                {
                    return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
                }
            }
        }
        else
        {
            var parsed = ScoreConfigurationInputParser.Parse(command.CalculationMethod, command.Components);
            if (parsed.IsFailure)
            {
                return Result.Failure<ScoreConfigurationDto>(parsed.Error);
            }

            var versionNo = checked(await configurationRepository.GetLatestVersionNoAsync(command.BatchId, cancellationToken) + 1);
            if (versionNo > 1 && string.IsNullOrWhiteSpace(command.ChangeReason))
            {
                return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ChangeReasonRequired);
            }

            try
            {
                draft = ScoreWeightConfig.CreateDraft(
                    Guid.NewGuid(),
                    command.BatchId,
                    versionNo,
                    parsed.Value.CalculationMethod,
                    parsed.Value.Components,
                    currentUser.UserId!.Value,
                    occurredAt,
                    command.ChangeReason);
            }
            catch (ArgumentException)
            {
                return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
            }
        }

        configurationRepository.Add(draft);
        auditOutbox.Add(
            AuditEntry(
                draft,
                IntegrationAuditActions.ScoreConfigurationDraftCreated,
                active is null ? null : new { ClonedFromConfigurationId = active.Id }),
            occurredAt);
        var saveError = await unitOfWork.TrySaveChangesAsync(
            ScoreConfigurationErrors.ConcurrencyConflict,
            ScoreConfigurationErrors.DraftAlreadyExists,
            cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ScoreConfigurationDto>(saveError);
        }

        return Result.Success(ScoreConfigurationMapper.ToDto(draft));
    }

    internal static AuditIntegrationEntry AuditEntry(
        ScoreWeightConfig configuration,
        string action,
        object? additionalMetadata = null)
        => new(
            "ScoreWeightConfig",
            configuration.Id.ToString(),
            action,
            "Succeeded",
            null,
            new
            {
                configuration.BatchId,
                configuration.VersionNo,
                Status = configuration.Status.ToString(),
                CalculationMethod = configuration.CalculationMethod.ToString(),
                configuration.ChangeReason,
                Components = configuration.Components.OrderBy(component => component.OrderIndex).Select(component => new
                {
                    component.ComponentCode,
                    component.Weight,
                    component.MinimumScore,
                    component.IsRequired,
                    component.OrderIndex
                }),
                Additional = additionalMetadata
            });
}

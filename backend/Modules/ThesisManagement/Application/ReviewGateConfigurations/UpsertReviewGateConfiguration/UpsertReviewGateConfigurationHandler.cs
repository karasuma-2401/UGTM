using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.UpsertReviewGateConfiguration;

public sealed class UpsertReviewGateConfigurationHandler(
    IKltnBatchReader batchReader,
    IReviewGateConfigurationRepository configurationRepository,
    IThesisManagementAuditOutbox auditOutbox,
    IThesisUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ReviewGateConfigurationDto>> HandleAsync(
        UpsertReviewGateConfigurationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanManageReviewGateConfigurations(currentUser))
        {
            return Result.Failure<ReviewGateConfigurationDto>(ReviewGateConfigurationErrors.AccessDenied);
        }

        if (command.BatchId == Guid.Empty || !Enum.IsDefined(command.GateType))
        {
            return Result.Failure<ReviewGateConfigurationDto>(ReviewGateConfigurationErrors.InvalidRequest);
        }

        if (!await batchReader.ExistsAsync(command.BatchId, cancellationToken))
        {
            return Result.Failure<ReviewGateConfigurationDto>(ReviewGateConfigurationErrors.BatchNotFound);
        }

        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var existingConfiguration = await configurationRepository.GetByBatchAndGateAsync(
            command.BatchId,
            command.GateType,
            cancellationToken);
        var isNew = existingConfiguration is null;
        ReviewGateConfiguration configuration;

        try
        {
            if (isNew)
            {
                configuration = new ReviewGateConfiguration(
                    Guid.NewGuid(),
                    command.BatchId,
                    command.GateType,
                    command.InitialSubmissionDeadlineUtc,
                    command.FacultyResponseDeadlineUtc,
                    command.FinalClosureDeadlineUtc,
                    currentUser.UserId!.Value,
                    occurredAt);
                configurationRepository.Add(configuration);
            }
            else
            {
                configuration = existingConfiguration!;
                configuration.UpdateDeadlines(
                    command.InitialSubmissionDeadlineUtc,
                    command.FacultyResponseDeadlineUtc,
                    command.FinalClosureDeadlineUtc,
                    currentUser.UserId!.Value,
                    occurredAt);
            }
        }
        catch (ArgumentException)
        {
            return Result.Failure<ReviewGateConfigurationDto>(ReviewGateConfigurationErrors.InvalidRequest);
        }

        auditOutbox.Add(
            AuditEntry(
                configuration,
                isNew
                    ? IntegrationAuditActions.ReviewGateConfigurationCreated
                    : IntegrationAuditActions.ReviewGateConfigurationUpdated),
            occurredAt);

        var saveError = await unitOfWork.TrySaveChangesAsync(
            ReviewGateConfigurationErrors.ConcurrencyConflict,
            ReviewGateConfigurationErrors.ConcurrencyConflict,
            cancellationToken);
        if (saveError is not null)
        {
            return Result.Failure<ReviewGateConfigurationDto>(saveError);
        }

        return Result.Success(ReviewGateConfigurationMapper.ToDto(configuration));
    }

    private static AuditIntegrationEntry AuditEntry(
        ReviewGateConfiguration configuration,
        string action)
        => new(
            "ReviewGateConfiguration",
            configuration.Id.ToString(),
            action,
            "Succeeded",
            null,
            new
            {
                configuration.BatchId,
                GateType = configuration.GateType.ToString(),
                configuration.InitialSubmissionDeadlineUtc,
                configuration.FacultyResponseDeadlineUtc,
                configuration.FinalClosureDeadlineUtc
            });
}

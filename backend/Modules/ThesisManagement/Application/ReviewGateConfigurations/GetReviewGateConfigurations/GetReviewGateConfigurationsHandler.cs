using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.GetReviewGateConfigurations;

public sealed class GetReviewGateConfigurationsHandler(
    IKltnBatchReader batchReader,
    IReviewGateConfigurationRepository configurationRepository,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ReviewGateConfigurationDto>>> HandleAsync(
        GetReviewGateConfigurationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!ThesisAuthorization.CanManageReviewGateConfigurations(currentUser))
        {
            return Result.Failure<IReadOnlyList<ReviewGateConfigurationDto>>(
                ReviewGateConfigurationErrors.AccessDenied);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ReviewGateConfigurationDto>>(
                ReviewGateConfigurationErrors.InvalidRequest);
        }

        if (!await batchReader.ExistsAsync(query.BatchId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ReviewGateConfigurationDto>>(
                ReviewGateConfigurationErrors.BatchNotFound);
        }

        var configurations = await configurationRepository.GetByBatchAsync(
            query.BatchId,
            cancellationToken);
        return Result.Success<IReadOnlyList<ReviewGateConfigurationDto>>(
            configurations
                .OrderBy(configuration => configuration.GateType)
                .Select(ReviewGateConfigurationMapper.ToDto)
                .ToArray());
    }
}

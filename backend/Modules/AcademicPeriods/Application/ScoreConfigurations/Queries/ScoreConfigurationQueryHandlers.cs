using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Application.Common;
using UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Queries;

public sealed class GetActiveScoreConfigurationHandler(
    IScoreConfigurationQueryReader queryReader,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser)
{
    public async Task<Result<ScoreConfigurationDto>> HandleAsync(
        GetActiveScoreConfigurationQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadScoreConfigurations(currentUser))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.AccessDenied);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (!await batchReader.ExistsAsync(query.BatchId, cancellationToken))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.BatchNotFound);
        }

        var configuration = await queryReader.GetActiveAsync(query.BatchId, cancellationToken);
        return configuration is null
            ? Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.ActiveNotFound)
            : Result.Success(configuration);
    }
}

public sealed class GetScoreConfigurationVersionHandler(
    IScoreConfigurationQueryReader queryReader,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser)
{
    public async Task<Result<ScoreConfigurationDto>> HandleAsync(
        GetScoreConfigurationVersionQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadScoreConfigurations(currentUser))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.AccessDenied);
        }

        if (query.BatchId == Guid.Empty || query.ConfigurationId == Guid.Empty)
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (!await batchReader.ExistsAsync(query.BatchId, cancellationToken))
        {
            return Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.BatchNotFound);
        }

        var configuration = await queryReader.GetByIdAsync(
            query.BatchId,
            query.ConfigurationId,
            cancellationToken);
        return configuration is null
            ? Result.Failure<ScoreConfigurationDto>(ScoreConfigurationErrors.NotFound)
            : Result.Success(configuration);
    }
}

public sealed class GetScoreConfigurationHistoryHandler(
    IScoreConfigurationQueryReader queryReader,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser)
{
    public async Task<Result<IReadOnlyList<ScoreConfigurationDto>>> HandleAsync(
        GetScoreConfigurationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!AcademicAuthorization.CanReadScoreConfigurations(currentUser))
        {
            return Result.Failure<IReadOnlyList<ScoreConfigurationDto>>(ScoreConfigurationErrors.AccessDenied);
        }

        if (query.BatchId == Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<ScoreConfigurationDto>>(ScoreConfigurationErrors.InvalidRequest);
        }

        if (!await batchReader.ExistsAsync(query.BatchId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<ScoreConfigurationDto>>(ScoreConfigurationErrors.BatchNotFound);
        }

        var configurations = await queryReader.GetHistoryAsync(query.BatchId, cancellationToken);
        return Result.Success(configurations);
    }
}

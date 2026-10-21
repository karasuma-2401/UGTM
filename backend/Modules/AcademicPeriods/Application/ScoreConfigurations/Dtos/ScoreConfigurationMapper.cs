using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Dtos;

public static class ScoreConfigurationMapper
{
    public static ScoreConfigurationDto ToDto(ScoreWeightConfig configuration)
        => new(
            configuration.Id,
            configuration.BatchId,
            configuration.VersionNo,
            configuration.Status.ToString(),
            configuration.CalculationMethod.ToString(),
            configuration.ChangeReason,
            configuration.CreatedBy,
            configuration.CreatedAt,
            configuration.UpdatedAt,
            configuration.PublishedBy,
            configuration.PublishedAt,
            configuration.RetiredAt,
            configuration.Components
                .OrderBy(component => component.OrderIndex)
                .Select(component => new ScoreComponentDto(
                    component.Id,
                    component.ComponentCode,
                    component.ComponentName,
                    component.Source.ToString(),
                    component.Applicability.ToString(),
                    component.Weight,
                    component.IsRequired,
                    component.MinimumScore,
                    component.RequiresApproval,
                    component.OrderIndex))
                .ToArray());
}

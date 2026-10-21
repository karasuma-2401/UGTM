namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Publish;

public sealed record PublishScoreConfigurationCommand(Guid BatchId, Guid ConfigurationId);

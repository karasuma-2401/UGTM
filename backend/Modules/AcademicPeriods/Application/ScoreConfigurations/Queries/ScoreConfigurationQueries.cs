namespace UGTM.Modules.AcademicPeriods.Application.ScoreConfigurations.Queries;

public sealed record GetActiveScoreConfigurationQuery(Guid BatchId);
public sealed record GetScoreConfigurationVersionQuery(Guid BatchId, Guid ConfigurationId);
public sealed record GetScoreConfigurationHistoryQuery(Guid BatchId);

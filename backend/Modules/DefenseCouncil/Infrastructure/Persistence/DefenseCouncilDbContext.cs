using UGTM.BuildingBlocks.Infrastructure.Persistence;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

public sealed class DefenseCouncilDbContext(DbContextOptions<DefenseCouncilDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => "councils";

    public DbSet<AdvisorEvaluation> AdvisorEvaluations => Set<AdvisorEvaluation>();
    public DbSet<Council> Councils => Set<Council>();
    public DbSet<CouncilAssignment> CouncilAssignments => Set<CouncilAssignment>();
    public DbSet<CouncilMember> CouncilMembers => Set<CouncilMember>();
    public DbSet<CouncilMinute> CouncilMinutes => Set<CouncilMinute>();
    public DbSet<CouncilScore> CouncilScores => Set<CouncilScore>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();
    public DbSet<GradingCriteria> GradingCriterias => Set<GradingCriteria>();
    public DbSet<ReviewerAssignment> ReviewerAssignments => Set<ReviewerAssignment>();
    public DbSet<ReviewerEvaluation> ReviewerEvaluations => Set<ReviewerEvaluation>();
    public DbSet<ScoreSummary> ScoreSummarys => Set<ScoreSummary>();
    public DbSet<ScoreSummaryComponent> ScoreSummaryComponents => Set<ScoreSummaryComponent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DefenseCouncilDbContext).Assembly);
    }
}

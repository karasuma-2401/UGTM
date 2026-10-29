using Microsoft.EntityFrameworkCore;
using Npgsql;
using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.DefenseCouncil.Application.Persistence;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

public sealed class DefenseCouncilDbContext(DbContextOptions<DefenseCouncilDbContext> options)
    : ModuleDbContext(options), IDefenseCouncilUnitOfWork
{
    public override string Schema => "councils";

    public DbSet<AuditOutboxMessage> AuditOutboxMessages => Set<AuditOutboxMessage>();
    public DbSet<AdvisorEvaluation> AdvisorEvaluations => Set<AdvisorEvaluation>();
    public DbSet<AdvisorEvaluationIdempotencyRecord> AdvisorEvaluationIdempotencyRecords => Set<AdvisorEvaluationIdempotencyRecord>();
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

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new DefenseCouncilConcurrencyException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } postgresException)
        {
            throw new DefenseCouncilUniqueConstraintException(postgresException.ConstraintName, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DefenseCouncilDbContext).Assembly);
    }
}

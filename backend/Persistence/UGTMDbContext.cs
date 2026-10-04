using Microsoft.EntityFrameworkCore;
using UGTM.Persistence.Entities.Academic;
using UGTM.Persistence.Entities.Councils;
using UGTM.Persistence.Entities.Identity;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence;

public class UGTMDbContext(DbContextOptions<UGTMDbContext> options) : DbContext(options)
{
    // ========================================================
    // MODULE 1: IDENTITY
    // ========================================================
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // ========================================================
    // MODULE 2: ACADEMIC
    // ========================================================
    public DbSet<KltnBatch> KltnBatches => Set<KltnBatch>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<ScoreWeightConfig> ScoreWeightConfigs => Set<ScoreWeightConfig>();
    public DbSet<RoleScoreWeight> RoleScoreWeights => Set<RoleScoreWeight>();

    // ========================================================
    // MODULE 3: TOPICS
    // ========================================================
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<StudentGroupMember> StudentGroupMembers => Set<StudentGroupMember>();

    // ========================================================
    // MODULE 4: THESIS
    // ========================================================
    public DbSet<ProgressMilestone> ProgressMilestones => Set<ProgressMilestone>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<Syllabus> Syllabuses => Set<Syllabus>();
    public DbSet<MidtermReport> MidtermReports => Set<MidtermReport>();
    public DbSet<RenameRequest> RenameRequests => Set<RenameRequest>();
    public DbSet<FinalReport> FinalReports => Set<FinalReport>();
    public DbSet<FinalSubmission> FinalSubmissions => Set<FinalSubmission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();

    // ========================================================
    // MODULE 5: COUNCILS
    // ========================================================
    public DbSet<GradingCriteria> GradingCriterias => Set<GradingCriteria>();
    public DbSet<AdvisorEvaluation> AdvisorEvaluations => Set<AdvisorEvaluation>();
    public DbSet<ReviewerAssignment> ReviewerAssignments => Set<ReviewerAssignment>();
    public DbSet<ReviewerEvaluation> ReviewerEvaluations => Set<ReviewerEvaluation>();
    public DbSet<Council> Councils => Set<Council>();
    public DbSet<CouncilMember> CouncilMembers => Set<CouncilMember>();
    public DbSet<CouncilAssignment> CouncilAssignments => Set<CouncilAssignment>();
    public DbSet<CouncilScore> CouncilScores => Set<CouncilScore>();
    public DbSet<CouncilMinute> CouncilMinutes => Set<CouncilMinute>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();
    public DbSet<ScoreSummary> ScoreSummaries => Set<ScoreSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UGTMDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Store all Enums as varchar(50) strings as decided in Q3
        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(50);
    }
}

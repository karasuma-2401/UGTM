using Microsoft.EntityFrameworkCore;
using UGTM.Persistence.Entities.Academic;
using UGTM.Persistence.Entities.Identity;
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
